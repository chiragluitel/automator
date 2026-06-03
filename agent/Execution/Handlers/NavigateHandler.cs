using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class NavigateHandler : IActionHandler
{
    public string Action => "navigate";

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var url = step.Target?.Url
            ?? throw new InvalidOperationException($"Step '{step.Id}' is missing target.url.");
        await ctx.GetWebSession().Page.GotoAsync(url);
    }
}
