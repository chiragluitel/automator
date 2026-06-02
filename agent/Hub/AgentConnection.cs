using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoFlow.Agent.Hub;

/// <summary>
/// Manages the outbound SignalR connection to the backend: dials out, registers,
/// listens for run dispatches, and streams step reports back.
/// </summary>
public class AgentConnection : IAsyncDisposable
{
    private readonly AgentOptions _opts;
    private readonly IStepExecutor _executor;
    private readonly ILogger<AgentConnection> _log;
    private HubConnection? _hub;

    public AgentConnection(IOptions<AgentOptions> opts, IStepExecutor executor, ILogger<AgentConnection> log)
    {
        _opts = opts.Value;
        _executor = executor;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var url = _opts.BackendUrl.TrimEnd('/') + _opts.HubPath;
        _hub = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<RunDispatchDto>("RunAutomation", dispatch => HandleRunAsync(dispatch));
        _hub.Reconnected += async _ => await RegisterAsync();

        await _hub.StartAsync(ct);
        await RegisterAsync();
        _log.LogInformation("Connected to {Url} as {Machine}", url, _opts.MachineName);
    }

    private Task RegisterAsync() =>
        _hub!.InvokeAsync("RegisterAgent", _opts.Token, _opts.UserEmail, _opts.MachineName);

    private async Task HandleRunAsync(RunDispatchDto dispatch)
    {
        _log.LogInformation("Run {RunId} received: {Name}", dispatch.RunId, dispatch.Definition.Name);

        bool success;
        try
        {
            success = await _executor.ExecuteAsync(dispatch.Definition, async report =>
                await _hub!.InvokeAsync("ReportStep",
                    new AgentStepReportDto(dispatch.RunId, report.StepId, report.StepOrder, report.Status, report.Message)));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Run {RunId} crashed", dispatch.RunId);
            await _hub!.InvokeAsync("RunCompleted",
                new AgentRunCompletedDto(dispatch.RunId, "Failed", ex.Message));
            return;
        }

        await _hub!.InvokeAsync("RunCompleted",
            new AgentRunCompletedDto(dispatch.RunId, success ? "Succeeded" : "Failed",
                success ? null : "One or more steps failed."));
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }
}
