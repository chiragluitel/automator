using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class ExtractHandler : IActionHandler
{
    public string Action => "extract";

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var page = ctx.GetWebSession().Page;
        var selector = HandlerHelpers.Selector(step);
        var attribute = HandlerHelpers.ParamOrNull(step, "attribute");
        var variableName = HandlerHelpers.Param(step, "variable");

        var value = attribute is not null
            ? await page.GetAttributeAsync(selector, attribute) ?? ""
            : await page.InnerTextAsync(selector);

        ctx.Variables[variableName] = value.Trim();
    }
}
