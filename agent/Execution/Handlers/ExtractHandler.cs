using System.Text.Json;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class ExtractHandler : IActionHandler
{
    public string Action => "extract";

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var variableName = HandlerHelpers.Param(step, "variable");
        var source       = HandlerHelpers.ParamOrNull(step, "source");

        if (source is not null)
        {
            // In-memory extraction: pull a named field out of a JSON object/array stored in a variable.
            var attribute = HandlerHelpers.ParamOrNull(step, "attribute");
            ctx.Variables[variableName] = ExtractFromJson(source, attribute);
            return;
        }

        // Web DOM extraction (original behaviour).
        var page      = ctx.GetWebSession().Page;
        var selector  = HandlerHelpers.Selector(step);
        var webAttr   = HandlerHelpers.ParamOrNull(step, "attribute");

        var value = webAttr is not null
            ? await page.GetAttributeAsync(selector, webAttr) ?? ""
            : await page.InnerTextAsync(selector);

        ctx.Variables[variableName] = value.Trim();
    }

    // Extracts a named attribute from a JSON value.
    // Object  → returns the attribute value as a string.
    // Array   → returns each element's attribute; if one result returns it plain, otherwise returns a JSON array.
    // Non-JSON or no attribute → returns the source string unchanged.
    private static string ExtractFromJson(string json, string? attribute)
    {
        if (attribute is null) return json;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var values = new List<string>();
                foreach (var el in root.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.Object &&
                        el.TryGetProperty(attribute, out var prop))
                        values.Add(prop.ValueKind == JsonValueKind.String
                            ? prop.GetString() ?? "" : prop.ToString());
                    else if (el.ValueKind == JsonValueKind.String)
                        values.Add(el.GetString() ?? "");
                }

                return values.Count switch
                {
                    0 => "",
                    1 => values[0],
                    _ => JsonSerializer.Serialize(values), // ["body1","body2",...]
                };
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(attribute, out var objProp))
                return objProp.ValueKind == JsonValueKind.String
                    ? objProp.GetString() ?? "" : objProp.ToString();
        }
        catch
        {
            // Not JSON — return as-is.
        }

        return json;
    }
}
