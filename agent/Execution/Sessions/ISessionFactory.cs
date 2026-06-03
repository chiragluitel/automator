namespace AutoFlow.Agent.Execution.Sessions;

public interface ISessionFactory
{
    bool CanCreate(string? app);

    // Returns the key under which the created session is stored in ExecutionContext.Sessions.
    string ResolveKey(string? app);

    Task<ISession> CreateAsync(CancellationToken ct = default);
}
