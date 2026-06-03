using System.Text.Json.Nodes;
using AutoFlow.Application.Abstractions;
using Json.Schema;

namespace AutoFlow.Infrastructure.Validation;

/// <summary>
/// Validates IR JSON against the automation IR contract.
/// The schema is embedded here so it is always available at runtime; the file
/// at contracts/automation-ir.schema.json is the human-facing source of truth.
/// </summary>
public class IrValidator : IIrValidator
{
    private readonly JsonSchema _schema = JsonSchema.FromText(SchemaText);

    public IReadOnlyList<string> Validate(string irJson)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(irJson);
        }
        catch (Exception ex)
        {
            return new[] { "IR is not valid JSON: " + ex.Message };
        }

        var results = _schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid) return Array.Empty<string>();

        var errors = new List<string>();
        Collect(results, errors);
        return errors.Count > 0 ? errors : new[] { "IR failed schema validation." };
    }

    private static void Collect(EvaluationResults results, List<string> sink)
    {
        if (results.Errors is { Count: > 0 })
            foreach (var (key, msg) in results.Errors)
                sink.Add($"{results.InstanceLocation}: {key} {msg}".Trim());

        foreach (var child in results.Details)
            Collect(child, sink);
    }

    private const string SchemaText = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["name", "schemaVersion", "trigger", "steps"],
      "additionalProperties": false,
      "properties": {
        "name": { "type": "string", "minLength": 1 },
        "schemaVersion": { "type": "integer", "const": 1 },
        "description": { "type": "string" },
        "trigger": {
          "type": "object",
          "required": ["type"],
          "additionalProperties": false,
          "properties": {
            "type": { "type": "string", "enum": ["manual","schedule","email_received","file_created","webhook"] },
            "source": { "type": ["string", "null"] },
            "schedule": { "type": ["string", "null"] },
            "conditions": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["field","op","value"],
                "additionalProperties": false,
                "properties": {
                  "field": { "type": "string" },
                  "op": { "type": "string", "enum": ["equals","contains","startsWith","matches","gt","lt"] },
                  "value": {}
                }
              }
            }
          }
        },
        "variables": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["name","type"],
            "additionalProperties": false,
            "properties": {
              "name": { "type": "string" },
              "type": { "type": "string", "enum": ["string","number","boolean","date"] },
              "from": { "type": ["string", "null"] },
              "value": {}
            }
          }
        },
        "steps": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "object",
            "required": ["id","order","action","rawInstruction"],
            "additionalProperties": false,
            "properties": {
              "id": { "type": "string" },
              "order": { "type": "integer", "minimum": 1 },
              "action": {
                "type": "string",
                "enum": ["open_application","navigate","click","type_text","select_option","read_email","extract","wait","condition","loop","api_call"]
              },
              "target": {
                "type": "object",
                "additionalProperties": true,
                "properties": {
                  "app": { "type": "string" },
                  "url": { "type": ["string","null"] },
                  "selector": { "type": ["string","null"] },
                  "label": { "type": ["string","null"] }
                }
              },
              "params": { "type": "object", "additionalProperties": true },
              "rawInstruction": { "type": "string" },
              "assetRefs": { "type": "array", "items": { "type": "string" } },
              "needsClarification": { "type": "boolean" },
              "clarificationQuestion": { "type": ["string","null"] }
            }
          }
        }
      }
    }
    """;
}
