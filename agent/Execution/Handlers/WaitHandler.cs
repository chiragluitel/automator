using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class WaitHandler : IActionHandler
{
    public string Action => "wait";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var ms = step.Params.TryGetValue("ms", out var v) && int.TryParse(v?.ToString(), out var n)
            ? n
            : 1000;
        return Task.Delay(ms, ctx.CancellationToken);
    }
}
