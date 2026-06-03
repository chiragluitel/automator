using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Triggers;

/// <summary>
/// Polls the user's Outlook Inbox (or a configured folder) every 30 seconds for
/// emails matching the trigger conditions.  When a match is found it fires the
/// provided callback exactly once per unique email (EntryID deduplication).
///
/// Design notes:
///   - Polling is used instead of COM events because COM application events require
///     an STA thread with a message pump, which is complex in a Worker Service.
///     30-second latency is completely acceptable for enterprise email automation.
///   - The watcher maintains a single OutlookSession across polls; if the session
///     becomes stale (Outlook restarted) it is discarded and recreated next poll.
///   - On first start we look back 60 seconds only, so historical emails never fire.
/// </summary>
public sealed class EmailTriggerWatcher : IAsyncDisposable
{
    private const int PollIntervalMs = 30_000;
    private const int StartupLookbackSeconds = 60;

    private readonly TriggerConfig _config;
    private readonly Func<TriggerFiredPayload, Task> _onFired;
    private readonly ILogger _log;

    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private OutlookSession? _session;
    private DateTime _lastCheckedAt;
    private readonly HashSet<string> _recentEntryIds = new(StringComparer.OrdinalIgnoreCase);

    public Guid TriggerId => _config.TriggerId;

    public EmailTriggerWatcher(TriggerConfig config, Func<TriggerFiredPayload, Task> onFired, ILogger log)
    {
        _config = config;
        _onFired = onFired;
        _log = log;
        _lastCheckedAt = DateTime.Now.AddSeconds(-StartupLookbackSeconds);
    }

    public void Start(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollTask = PollLoopAsync(_cts.Token);
        _log.LogInformation("Trigger {TriggerId} watcher started (type={Type})", _config.TriggerId, _config.Type);
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Trigger {TriggerId} poll failed — will retry next interval", _config.TriggerId);
                // Invalidate the session so a fresh COM connection is attempted next time.
                DisposeSession();
            }

            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var session = await GetOrCreateSessionAsync(ct);
        var folder = session.GetFolder(
            _config.Conditions.TryGetValue("folder", out var f) && !string.IsNullOrWhiteSpace(f) ? f : null);

        var cutoff = _lastCheckedAt;
        var now = DateTime.Now;

        // Build a JET filter for the date window — reduces COM round-trips significantly.
        var jetFilter = BuildDateJetFilter(cutoff);
        dynamic items;
        try
        {
            items = string.IsNullOrEmpty(jetFilter)
                ? folder.Items
                : folder.Items.Restrict(jetFilter);
        }
        catch
        {
            items = folder.Items;
        }

        // Sort descending so we process newest first (better UX if several arrived at once).
        try { items.Sort("[ReceivedTime]", true); } catch { }

        int count = 0;
        try { count = (int)items.Count; } catch { return; }

        var toFire = new List<(string EntryId, string EmailJson)>();

        for (var i = 1; i <= count; i++)
        {
            dynamic? item = null;
            try
            {
                item = items[i];
                int mailClass = 0;
                try { mailClass = (int)item.Class; } catch { }
                if (mailClass != 43) continue; // 43 = olMail

                string entryId = "";
                try { entryId = (string)(item.EntryID ?? ""); } catch { }
                if (string.IsNullOrEmpty(entryId) || _recentEntryIds.Contains(entryId)) continue;

                if (!MatchesConditions(item)) continue;

                var emailData = EmailItemSerializer.Serialize(item);
                var emailJson = System.Text.Json.JsonSerializer.Serialize(emailData);
                toFire.Add((entryId, emailJson));
            }
            catch { }
            finally
            {
                if (item is not null)
                    try { System.Runtime.InteropServices.Marshal.ReleaseComObject(item); } catch { }
            }
        }

        _lastCheckedAt = now;

        foreach (var (entryId, emailJson) in toFire)
        {
            _recentEntryIds.Add(entryId);
            CapRecentSet();

            _log.LogInformation("Trigger {TriggerId} fired for automation {AutomationId} (entryId={EntryId})",
                _config.TriggerId, _config.AutomationId, entryId[..Math.Min(8, entryId.Length)]);

            await _onFired(new TriggerFiredPayload(
                _config.TriggerId,
                _config.AutomationId,
                new Dictionary<string, string> { ["triggerEmail"] = emailJson }
            ));
        }
    }

    // ── Condition evaluation ─────────────────────────────────────────────────

    private bool MatchesConditions(dynamic mail)
    {
        foreach (var (key, value) in _config.Conditions)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            switch (key.ToLowerInvariant())
            {
                case "from":
                    if (!ComStringContains(mail, "SenderEmailAddress", value) &&
                        !ComStringContains(mail, "SenderName", value))
                        return false;
                    break;
                case "subject_contains":
                    if (!ComStringContains(mail, "Subject", value)) return false;
                    break;
                case "body_contains":
                    if (!ComStringContains(mail, "Body", value)) return false;
                    break;
                case "to":
                    if (!ComStringContains(mail, "To", value)) return false;
                    break;
                case "cc":
                    if (!ComStringContains(mail, "CC", value)) return false;
                    break;
                case "category":
                    if (!ComStringContains(mail, "Categories", value)) return false;
                    break;
                case "importance":
                    if (!MatchesImportance(mail, value)) return false;
                    break;
                case "is_unread":
                    if (bool.TryParse(value, out var wantUnread))
                    {
                        bool actualUnread = true;
                        try { actualUnread = (bool)mail.UnRead; } catch { }
                        if (wantUnread != actualUnread) return false;
                    }
                    break;
                case "is_flagged":
                    if (bool.TryParse(value, out var wantFlagged))
                    {
                        bool actualFlagged = false;
                        try { actualFlagged = (int)mail.FlagStatus == 2; } catch { }
                        if (wantFlagged != actualFlagged) return false;
                    }
                    break;
                case "has_attachment":
                    if (bool.TryParse(value, out var wantAttach))
                    {
                        bool actualAttach = false;
                        try { actualAttach = (int)mail.Attachments.Count > 0; } catch { }
                        if (wantAttach != actualAttach) return false;
                    }
                    break;
                // folder is handled at the folder-selection level, not per-item
            }
        }
        return true;
    }

    private static bool ComStringContains(dynamic mail, string property, string value)
    {
        try
        {
            var raw = (string)(mail.GetType().InvokeMember(property,
                System.Reflection.BindingFlags.GetProperty, null, mail, null) ?? "");
            return raw.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesImportance(dynamic mail, string value)
    {
        int importance = 1;
        try { importance = (int)mail.Importance; } catch { return true; }
        return value.ToLowerInvariant() switch
        {
            "high" => importance == 2,
            "low"  => importance == 0,
            _      => importance == 1
        };
    }

    // ── JET filter builder ───────────────────────────────────────────────────

    private static string BuildDateJetFilter(DateTime from)
    {
        // Outlook JET date format: M/d/yyyy H:mm AM/PM  (locale-sensitive on the Outlook side)
        // Using invariant format with explicit pattern that Outlook accepts.
        var fromStr = from.ToString("MM/dd/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return $"[ReceivedTime] >= '{fromStr}'";
    }

    // ── Session management ───────────────────────────────────────────────────

    private async Task<OutlookSession> GetOrCreateSessionAsync(CancellationToken ct)
    {
        if (_session is not null) return _session;
        _session = await OutlookSession.CreateAsync(ct, log: _log);
        return _session;
    }

    private void DisposeSession()
    {
        try { _session?.DisposeAsync().AsTask().Wait(1_000); } catch { }
        _session = null;
    }

    private void CapRecentSet()
    {
        // Prevent unbounded growth — keep the last 500 seen entry IDs.
        if (_recentEntryIds.Count > 500)
        {
            var first = _recentEntryIds.First();
            _recentEntryIds.Remove(first);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_pollTask is not null)
            try { await _pollTask.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        DisposeSession();
        _cts?.Dispose();
        _log.LogInformation("Trigger {TriggerId} watcher stopped", _config.TriggerId);
    }
}

/// <summary>Internal payload passed from the watcher up to AgentConnection.</summary>
public record TriggerFiredPayload(
    Guid TriggerId,
    Guid AutomationId,
    Dictionary<string, string> InitialVariables
);
