using System.Globalization;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class SetCellHandler : IActionHandler
{
    public string Action => "set_cell";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var session = HandlerHelpers.GetOfficeSession(step, ctx);
        var sheet = session.GetWorksheet(step.Target?.Sheet);
        var cellRef = HandlerHelpers.Param(step, "cell");
        var value = HandlerHelpers.Param(step, "value");

        var cell = sheet.Cell(cellRef);
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            cell.Value = d;
        else if (bool.TryParse(value, out var b))
            cell.Value = b;
        else
            cell.Value = value;

        return Task.CompletedTask;
    }
}
