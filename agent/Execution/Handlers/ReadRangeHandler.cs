using System.Text.Json;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class ReadRangeHandler : IActionHandler
{
    public string Action => "read_range";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var session = HandlerHelpers.GetOfficeSession(step, ctx);
        var sheet = session.GetWorksheet(step.Target?.Sheet);
        var rangeRef = HandlerHelpers.Param(step, "range");
        var variable = HandlerHelpers.Param(step, "variable");

        var xlRange = sheet.Range(rangeRef);
        var data = xlRange.Rows()
            .Select(row => row.Cells()
                .Select(c => c.Value.IsBlank ? "" : c.Value.ToString())
                .ToArray())
            .ToArray();

        ctx.Variables[variable] = JsonSerializer.Serialize(data);
        return Task.CompletedTask;
    }
}
