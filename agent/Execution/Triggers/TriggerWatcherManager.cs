using System.Collections.Concurrent;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Triggers;

/// <summary>
/// Manages the lifecycle of all active trigger watchers for the connected user.
///
/// Lifecycle:
///   1. Backend pushes trigger configs via "PushTriggers" on agent connect.
///   2. <see cref="ConfigureTriggers"/> starts a watcher per email_received config.
///   3. Each watcher polls Outlook and calls back when a condition fires.
///   4. The callback sends "TriggerFired" to the hub.
///   5. On reconnect the backend pushes configs again — existing watchers are
///      reconciled (new ones started, removed ones stopped).
/// </summary>
public sealed class TriggerWatcherManager : IAsyncDisposable
{
    private readonly ILogger<TriggerWatcherManager> _log;
    private readonly ConcurrentDictionary<Guid, EmailTriggerWatcher> _watchers = new();
    private Func<TriggerFiredPayload, Task>? _onFired;
    private CancellationToken _runCt = CancellationToken.None;

    public TriggerWatcherManager(ILogger<TriggerWatcherManager> log) => _log = log;

    /// <summary>
    /// Sets the callback that is invoked when any watcher fires.
    /// Must be set before <see cref="ConfigureTriggers"/> is called.
    /// </summary>
    public void SetFiredCallback(Func<TriggerFiredPayload, Task> callback) => _onFired = callback;

    /// <summary>
    /// Reconciles the running watchers against the new config list received from
    /// the backend.  Stops watchers whose trigger was removed or deactivated;
    /// starts watchers for new trigger IDs.
    /// </summary>
    public void ConfigureTriggers(IReadOnlyList<TriggerConfig> configs, CancellationToken ct)
    {
        _runCt = ct;
        var activeIds = new HashSet<Guid>(configs.Select(c => c.TriggerId));

        // Stop any watcher no longer in the new config.
        foreach (var (id, watcher) in _watchers)
        {
            if (activeIds.Contains(id)) continue;
            if (_watchers.TryRemove(id, out var removed))
                _ = removed.DisposeAsync().AsTask();
        }

        // Start watchers for newly added configs.
        foreach (var config in configs)
        {
            if (config.Type != "email_received") continue;
            if (_watchers.ContainsKey(config.TriggerId)) continue;

            var watcher = new EmailTriggerWatcher(config, FireAsync, _log);
            if (_watchers.TryAdd(config.TriggerId, watcher))
                watcher.Start(ct);
        }

        _log.LogInformation("Trigger watchers configured: {Count} active", _watchers.Count);
    }

    private async Task FireAsync(TriggerFiredPayload payload)
    {
        if (_onFired is null)
        {
            _log.LogWarning("Trigger {TriggerId} fired but no callback registered — ignoring", payload.TriggerId);
            return;
        }
        try { await _onFired(payload); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Trigger {TriggerId} fired callback threw", payload.TriggerId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var tasks = _watchers.Values.Select(w => w.DisposeAsync().AsTask()).ToList();
        await Task.WhenAll(tasks);
        _watchers.Clear();
    }
}
