namespace AutoFlow.Agent.Execution.Sessions;

public interface ISession : IAsyncDisposable
{
    // Called by RunExecutor after all steps complete.
    // WebSession: waits for user to close the browser in headed mode.
    // All others: no-op.
    Task WaitForCloseIfNeededAsync(CancellationToken ct);
}
