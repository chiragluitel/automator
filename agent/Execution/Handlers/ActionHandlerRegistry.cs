using System.Diagnostics.CodeAnalysis;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class ActionHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IActionHandler> _handlers;

    public ActionHandlerRegistry(IEnumerable<IActionHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Action, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetHandler(string action, [NotNullWhen(true)] out IActionHandler? handler) =>
        _handlers.TryGetValue(action, out handler);
}
