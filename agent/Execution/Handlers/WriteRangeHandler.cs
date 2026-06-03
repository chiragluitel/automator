using System.Globalization;
using System.Text.Json;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class WriteRangeHandler : IActionHandler
{
    public string Action => "write_range";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var session   = HandlerHelpers.GetOfficeSession(step, ctx);
        var sheet     = session.GetWorksheet(step.Target?.Sheet);
        var rangeRef  = HandlerHelpers.Param(step, "range");
        var rawValues = step.Params.TryGetValue("values", out var v) ? v : null;

        var xlRange   = sheet.Range(rangeRef);
        var originRow = xlRange.FirstCell().Address.RowNumber;
        var originCol = xlRange.FirstCell().Address.ColumnNumber;

        var rows = ParseValues(rawValues);
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                WriteCell(sheet.Cell(originRow + r, originCol + c), rows[r][c]);

        return Task.CompletedTask;
    }

    // Normalises the raw params.values into a 2-D string grid.
    // Accepts: 2-D JSON array, 1-D JSON array (becomes one column), or a plain scalar / string.
    private static string[][] ParseValues(object? raw)
    {
        var json = JsonSerializer.Serialize(raw);

        try
        {
            // Try 2-D array first.
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var first = doc.RootElement.EnumerateArray().FirstOrDefault();

                if (first.ValueKind == JsonValueKind.Array)
                {
                    // [[...],[...]] — proper 2-D array.
                    return doc.RootElement.EnumerateArray()
                        .Select(row => row.EnumerateArray()
                            .Select(el => el.ValueKind == JsonValueKind.String
                                ? el.GetString()! : el.ToString())
                            .ToArray())
                        .ToArray();
                }

                // ["a","b","c"] — 1-D array → one value per row.
                return doc.RootElement.EnumerateArray()
                    .Select(el => new[]
                    {
                        el.ValueKind == JsonValueKind.String ? el.GetString()! : el.ToString()
                    })
                    .ToArray();
            }

            // JSON string primitive.
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return new[] { new[] { doc.RootElement.GetString()! } };

            // Any other JSON primitive (number, bool, null).
            return new[] { new[] { doc.RootElement.ToString() } };
        }
        catch
        {
            // Not parseable JSON → treat as a plain string scalar.
            return new[] { new[] { raw?.ToString() ?? "" } };
        }
    }

    private static void WriteCell(ClosedXML.Excel.IXLCell cell, string text)
    {
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            cell.Value = d;
        else if (bool.TryParse(text, out var b))
            cell.Value = b;
        else
            cell.Value = text;
    }
}
