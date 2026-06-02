using AutoFlow.Agent.Hub;

namespace AutoFlow.Agent;

public class Worker : BackgroundService
{
    private readonly AgentConnection _connection;
    private readonly ILogger<Worker> _log;

    public Worker(AgentConnection connection, ILogger<Worker> log)
    {
        _connection = connection;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _connection.StartAsync(stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Agent failed to start. Is the backend running at the configured URL?");
        }
    }
}
