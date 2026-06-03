using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class TypeTextHandler : IActionHandler
{
    public string Action => "type_text";

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx) =>
        await ctx.GetWebSession().Page.FillAsync(
            HandlerHelpers.Selector(step),
            HandlerHelpers.Param(step, "text"));
}
