using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class ClickHandler : IActionHandler
{
    public string Action => "click";

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx) =>
        await ctx.GetWebSession().Page.ClickAsync(HandlerHelpers.Selector(step));
}
