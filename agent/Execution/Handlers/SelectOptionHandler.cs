using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class SelectOptionHandler : IActionHandler
{
    public string Action => "select_option";

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx) =>
        await ctx.GetWebSession().Page.SelectOptionAsync(
            HandlerHelpers.Selector(step),
            HandlerHelpers.Param(step, "value"));
}
