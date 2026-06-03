using System.Collections.Concurrent;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace AutoFlow.Api.Realtime;

/// <summary>
/// Realtime channel for agents. Agents dial in (outbound), authenticate with a
/// shared token (MVP), and stream step reports back. The server pushes run
/// dispatches and trigger configs to the agent's connection.
/// </summary>
public class AgentHub : Hub
{
    private readonly IAgentConnectionTracker _tracker;
    private readonly IRunService _runs;
    private readonly ITriggerService _triggers;
    private readonly string _sharedToken;

    public AgentHub(IAgentConnectionTracker tracker, IRunService runs, ITriggerService triggers, IConfiguration config)
    {
        _tracker = tracker;
        _runs = runs;
        _triggers = triggers;
        _sharedToken = config["Agents:SharedToken"] ?? string.Empty;
    }

    public async Task RegisterAgent(string token, string userEmail, string machineName)
    {
        if (string.IsNullOrEmpty(_sharedToken) || token != _sharedToken)
        {
            Context.Abort();
            return;
        }

        _tracker.Register(userEmail, Context.ConnectionId);
        await Clients.Caller.SendAsync("Registered", machineName);

        // Push any active trigger configs so the agent starts watching immediately.
        var configs = await _triggers.GetActiveForUserEmailAsync(userEmail);
        if (configs.Count > 0)
            await Clients.Caller.SendAsync("PushTriggers", configs);
    }

    public Task ReportStep(AgentStepReportDto report) => _runs.RecordStepAsync(report);

    public Task RunCompleted(AgentRunCompletedDto completed) => _runs.CompleteAsync(completed);

    /// <summary>
    /// Called by the agent when a watched condition fires (e.g. an email arrives).
    /// Creates a run and dispatches the IR back to the agent with the trigger context.
    /// </summary>
    public async Task TriggerFired(TriggerFiredDto fired)
    {
        var userEmail = _tracker.GetUserForConnection(Context.ConnectionId);
        if (userEmail is null) return;

        try
        {
            await _runs.TriggerByAgentAsync(fired.AutomationId, userEmail, fired.InitialVariables);
        }
        catch (Exception ex)
        {
            // Log but don't crash the hub — the next trigger fire will retry.
            await Clients.Caller.SendAsync("TriggerError",
                $"Failed to start run for automation {fired.AutomationId}: {ex.Message}");
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>In-memory mapping of user ↔ agent connection. Single instance only;
/// a multi-instance deployment needs a SignalR backplane (e.g. Redis).</summary>
public class InMemoryAgentConnectionTracker : IAgentConnectionTracker
{
    private readonly ConcurrentDictionary<string, string> _userToConn = new();
    private readonly ConcurrentDictionary<string, string> _connToUser = new();

    public void Register(string userEmail, string connectionId)
    {
        _userToConn[userEmail] = connectionId;
        _connToUser[connectionId] = userEmail;
    }

    public void Remove(string connectionId)
    {
        if (_connToUser.TryRemove(connectionId, out var email))
            _userToConn.TryRemove(email, out _);
    }

    public string? GetConnectionForUser(string userEmail) =>
        _userToConn.TryGetValue(userEmail, out var conn) ? conn : null;

    public string? GetUserForConnection(string connectionId) =>
        _connToUser.TryGetValue(connectionId, out var email) ? email : null;
}

/// <summary>Pushes run dispatches to the agent via SignalR.</summary>
public class SignalRAgentDispatcher : IAgentDispatcher
{
    private readonly IHubContext<AgentHub> _hub;
    private readonly IAgentConnectionTracker _tracker;

    public SignalRAgentDispatcher(IHubContext<AgentHub> hub, IAgentConnectionTracker tracker)
    {
        _hub = hub;
        _tracker = tracker;
    }

    public async Task<bool> DispatchAsync(string userEmail, RunDispatchDto dispatch, CancellationToken ct = default)
    {
        var conn = _tracker.GetConnectionForUser(userEmail);
        if (conn is null) return false;
        await _hub.Clients.Client(conn).SendAsync("RunAutomation", dispatch, ct);
        return true;
    }
}

/// <summary>MVP identity. Replace with a claims-based implementation under SSO.</summary>
public class DemoCurrentUser : ICurrentUser
{
    public Guid UserId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string Email => "demo@amcor.com";
}
