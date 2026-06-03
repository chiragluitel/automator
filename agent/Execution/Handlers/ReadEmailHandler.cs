using System.Runtime.InteropServices;
using System.Text.Json;
using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Reads emails from classic Outlook via COM.
///
/// Supported params:
///   folder         — Inbox (default), Sent Items, Drafts, Deleted Items, Outbox, Junk Email,
///                    Archive, or any custom folder name / slash-separated path (e.g. "Projects/Q2").
///   from           — partial match on sender email address or display name.
///   to             — partial match on To: recipients.
///   cc             — partial match on CC: recipients.
///   subject        — exact subject match (case-insensitive).
///   subject_contains — partial subject match (case-insensitive).
///   body_contains  — full-text body scan (slow; use sparingly).
///   category       — partial match on Outlook category label.
///   date_range     — named shorthand: today, yesterday, this_week, last_week, this_month,
///                    last_month, this_year, last_year, last_7_days, last_14_days, last_30_days,
///                    last_60_days, last_90_days, last_6_months.
///   date_from      — ISO date lower bound (e.g. "2026-06-01").
///   date_to        — ISO date upper bound (inclusive, e.g. "2026-06-30").
///   last_minutes   — integer; emails received within the last N minutes.
///   last_hours     — integer; emails received within the last N hours.
///   last_days      — integer; emails received within the last N days.
///   is_unread      — "true"/"false"; filter by read status.
///   is_flagged     — "true"/"false"; filter by flag status.
///   has_attachment — "true"/"false"; filter by attachment presence.
///   importance     — "high", "normal", "low".
///   limit          — maximum number of emails to return (newest first).
///   variable       — REQUIRED. Name of the ctx variable to store results.
///                    Single email → JSON object; multiple → JSON array.
///   include_body   — "false" to omit body content (faster when you only need metadata).
/// </summary>
public sealed class ReadEmailHandler : IActionHandler
{
    private readonly ILogger<ReadEmailHandler> _log;
    public string Action => "read_email";

    public ReadEmailHandler(ILogger<ReadEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var folder      = HandlerHelpers.ParamOrNull(step, "folder") ?? "Inbox";
        var variable    = HandlerHelpers.ParamOrNull(step, "variable") ?? "emails";
        var limit       = int.TryParse(HandlerHelpers.ParamOrNull(step, "limit"), out var l) ? l : int.MaxValue;
        var includeBody = !string.Equals(HandlerHelpers.ParamOrNull(step, "include_body"), "false",
                              StringComparison.OrdinalIgnoreCase);
        var hasBodyFilter = HandlerHelpers.ParamOrNull(step, "body_contains") is not null;

        if (hasBodyFilter)
            _log.LogWarning("body_contains performs a full-text scan of each email body — this can be slow for large folders.");

        var session = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        _log.LogInformation("Reading emails from folder '{Folder}'", folder);

        var results = new List<Dictionary<string, object?>>();
        dynamic? outlookFolder  = null;
        dynamic? items          = null;
        dynamic? restricted     = null;

        try
        {
            outlookFolder = session.GetFolder(folder);
            items         = outlookFolder.Items;
            items.Sort("[ReceivedTime]", true); // newest first

            var jetFilter = OutlookFilterBuilder.BuildJetFilter(step);
            var iterSource = string.IsNullOrEmpty(jetFilter)
                ? (object)items
                : items.Restrict(jetFilter);

            if (!string.IsNullOrEmpty(jetFilter))
                restricted = iterSource as dynamic;

            foreach (dynamic mail in (dynamic)iterSource)
            {
                if (results.Count >= limit) { OutlookComHelper.Release(mail); break; }

                try
                {
                    // Skip non-mail items (meeting requests, delivery reports, etc.)
                    int cls;
                    try { cls = (int)mail.Class; } catch { cls = 0; }
                    if (cls != 43) // 43 = olMail
                    {
                        OutlookComHelper.Release(mail);
                        continue;
                    }

                    if (!OutlookFilterBuilder.MatchesInMemoryFilters(mail, step))
                    {
                        OutlookComHelper.Release(mail);
                        continue;
                    }

                    var serialized = EmailItemSerializer.Serialize(mail);
                    if (!includeBody)
                    {
                        serialized.Remove("body");
                        serialized.Remove("htmlBody");
                    }
                    results.Add(serialized);
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Skipping mail item that could not be serialised.");
                }
                finally
                {
                    OutlookComHelper.Release(mail);
                }
            }
        }
        finally
        {
            if (restricted is not null) OutlookComHelper.Release(restricted);
            if (items      is not null) OutlookComHelper.Release(items);
            if (outlookFolder is not null) OutlookComHelper.Release(outlookFolder);
        }

        // limit=1 stores a single object for ergonomic variable use;
        // all other cases store an array.
        var json = EmailItemSerializer.SerializeToJson(results, singleObject: limit == 1);
        ctx.Variables[variable] = json;

        _log.LogInformation("Stored {Count} email(s) in variable '{Variable}'", results.Count, variable);
    }
}
