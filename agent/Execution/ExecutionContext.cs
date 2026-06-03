using AutoFlow.Agent.Execution.Sessions;

namespace AutoFlow.Agent.Execution;

public sealed class ExecutionContext
{
    public Guid RunId { get; }
    public CancellationToken CancellationToken { get; }

    // Populated by ExtractHandler; resolved by VariableResolver before each step.
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Keyed by surface name ("web", "desktop", …); lazily created by OpenApplicationHandler.
    public Dictionary<string, ISession> Sessions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ExecutionContext(Guid runId, CancellationToken cancellationToken = default)
    {
        RunId = runId;
        CancellationToken = cancellationToken;
    }

    public IWebSession GetWebSession() =>
        Sessions.TryGetValue(WebSession.Key, out var s) && s is IWebSession ws
            ? ws
            : throw new InvalidOperationException(
                "No browser open. Add an 'open_application' step first.");
}
