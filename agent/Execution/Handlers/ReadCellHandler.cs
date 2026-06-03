using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class ReadCellHandler : IActionHandler
{
    public string Action => "read_cell";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var session = HandlerHelpers.GetOfficeSession(step, ctx);
        var sheet = session.GetWorksheet(step.Target?.Sheet);
        var cellRef = HandlerHelpers.Param(step, "cell");
        var variable = HandlerHelpers.Param(step, "variable");

        var cell = sheet.Cell(cellRef);
        ctx.Variables[variable] = cell.Value.IsBlank ? "" : cell.Value.ToString();
        return Task.CompletedTask;
    }
}
