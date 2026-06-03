using System.Globalization;
using System.Text.Json;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class WriteRangeHandler : IActionHandler
{
    public string Action => "write_range";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var session = HandlerHelpers.GetOfficeSession(step, ctx);
        var sheet = session.GetWorksheet(step.Target?.Sheet);
        var rangeRef = HandlerHelpers.Param(step, "range");

        // values param may arrive as a JsonElement or a JSON string; normalize via serialize+parse.
        var rawValues = step.Params.TryGetValue("values", out var v) ? v : null;
        var json = JsonSerializer.Serialize(rawValues);
        var rows = JsonSerializer.Deserialize<JsonElement[][]>(json)
                   ?? throw new InvalidOperationException($"Step '{step.Id}' params.values must be a 2-D JSON array.");

        var xlRange = sheet.Range(rangeRef);
        var originRow = xlRange.FirstCell().Address.RowNumber;
        var originCol = xlRange.FirstCell().Address.ColumnNumber;

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = sheet.Cell(originRow + r, originCol + c);
                var text = rows[r][c].ValueKind == JsonValueKind.String
                    ? rows[r][c].GetString()!
                    : rows[r][c].ToString();

                if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    cell.Value = d;
                else if (bool.TryParse(text, out var b))
                    cell.Value = b;
                else
                    cell.Value = text;
            }
        }

        return Task.CompletedTask;
    }
}
