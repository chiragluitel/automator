using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using AutoFlow.Application.Ir;
using Microsoft.Extensions.Options;

namespace AutoFlow.Infrastructure.Claude;

/// <summary>
/// Compiles authored steps + screenshots into the IR using the Anthropic Messages API.
/// We force structured output via a single tool ("emit_automation_ir") whose input_schema
/// mirrors the IR contract, so the model returns schema-shaped JSON rather than prose.
/// </summary>
public class ClaudeCompilationService : ICompilationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ClaudeOptions _opts;

    public ClaudeCompilationService(HttpClient http, IOptions<ClaudeOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public async Task<AutomationIr> CompileAsync(CompileRequestDto request, CancellationToken ct = default)
    {
        var content = BuildUserContent(request);

        var payload = new
        {
            model = _opts.Model,
            max_tokens = _opts.MaxTokens,
            system = SystemPrompt,
            tools = new object[]
            {
                new
                {
                    name = "emit_automation_ir",
                    description = "Return the automation as an IR document conforming to the AutoFlow schema.",
                    input_schema = JsonNode.Parse(ToolInputSchema)
                }
            },
            tool_choice = new { type = "tool", name = "emit_automation_ir" },
            messages = new object[]
            {
                new { role = "user", content }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, _opts.BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("x-api-key", _opts.ApiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", _opts.AnthropicVersion);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Claude API error {(int)resp.StatusCode}: {body}");

        return ExtractIr(body);
    }

    private static List<object> BuildUserContent(CompileRequestDto request)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "text",
                text =
                    $"Automation name: {request.Name}\n" +
                    $"Description: {request.Description ?? "(none)"}\n" +
                    $"Trigger hint: {request.TriggerHint ?? "(none)"}\n\n" +
                    "Authored steps follow in order. A screenshot (if provided) appears immediately after its step."
            }
        };

        for (var i = 0; i < request.Steps.Count; i++)
        {
            var step = request.Steps[i];
            blocks.Add(new { type = "text", text = $"Step {i + 1}: {step.RawInstruction}" });

            if (!string.IsNullOrWhiteSpace(step.ScreenshotBase64))
            {
                blocks.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = step.ScreenshotMediaType ?? "image/png",
                        data = Strip(step.ScreenshotBase64!)
                    }
                });
            }
        }

        if (request.Answers is { Count: > 0 })
        {
            var sb = new StringBuilder("Clarification answers (incorporate these and set needsClarification=false for the matching steps):\n");
            foreach (var a in request.Answers)
                sb.AppendLine($"- step {a.StepId}: {a.Answer}");
            blocks.Add(new { type = "text", text = sb.ToString() });
        }

        blocks.Add(new { type = "text", text = "Now call emit_automation_ir with the complete IR." });
        return blocks;
    }

    private static AutomationIr ExtractIr(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("content", out var content))
            throw new InvalidOperationException("Claude response had no content array.");

        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "tool_use" &&
                block.TryGetProperty("name", out var name) && name.GetString() == "emit_automation_ir" &&
                block.TryGetProperty("input", out var input))
            {
                return JsonSerializer.Deserialize<AutomationIr>(input.GetRawText(), Json)
                    ?? throw new InvalidOperationException("Tool input deserialized to null.");
            }
        }

        throw new InvalidOperationException("Claude did not return an emit_automation_ir tool call.");
    }

    private static string Strip(string b64)
    {
        var comma = b64.IndexOf(',');
        return b64.StartsWith("data:") && comma >= 0 ? b64[(comma + 1)..] : b64;
    }

    private const string SystemPrompt = """
    You convert a non-technical user's authored automation (ordered free-text steps plus optional UI screenshots) into a structured Automation IR for the AutoFlow runtime.

    Rules:
    - Call the emit_automation_ir tool exactly once with the full IR. Do not write prose.
    - schemaVersion is always 1. Give steps ids "s1", "s2", ... and sequential order starting at 1.
    - Preserve each step's original text verbatim in rawInstruction.
    - Map the trigger hint to a trigger.type: an email arriving -> "email_received" (source "outlook"); a time/schedule -> "schedule" (set a cron in schedule); nothing clear -> "manual". Add conditions when the hint implies them (e.g. subject contains X).
    - Only use these actions: open_application, navigate, click, type_text, select_option, read_email, extract, wait, condition, loop, api_call.
    - Use screenshots to infer concrete targets (URLs, selectors, field labels). When a step lacks the specifics needed to run it reliably (e.g. a URL or which field to use) and you cannot infer it, set needsClarification=true and write one precise clarificationQuestion asking for exactly what you need.
    - Prefer stable web selectors (role/name, label text) over brittle ones. Put human-visible labels in target.label as a fallback.
    - Reference values produced earlier with {{variableName}} in params, and declare those in variables.
    """;

    // Mirrors the IR contract; guides the model. Strict validation happens server-side after.
    private const string ToolInputSchema = """
    {
      "type": "object",
      "required": ["name", "schemaVersion", "trigger", "steps"],
      "properties": {
        "name": { "type": "string" },
        "schemaVersion": { "type": "integer" },
        "description": { "type": "string" },
        "trigger": {
          "type": "object",
          "required": ["type"],
          "properties": {
            "type": { "type": "string", "enum": ["manual","schedule","email_received","file_created","webhook"] },
            "source": { "type": "string" },
            "schedule": { "type": "string" },
            "conditions": {
              "type": "array",
              "items": {
                "type": "object",
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
            "properties": {
              "name": { "type": "string" },
              "type": { "type": "string", "enum": ["string","number","boolean","date"] },
              "from": { "type": "string" }
            }
          }
        },
        "steps": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["id","order","action","rawInstruction"],
            "properties": {
              "id": { "type": "string" },
              "order": { "type": "integer" },
              "action": { "type": "string", "enum": ["open_application","navigate","click","type_text","select_option","read_email","extract","wait","condition","loop","api_call"] },
              "target": {
                "type": "object",
                "properties": {
                  "app": { "type": "string" },
                  "url": { "type": ["string","null"] },
                  "selector": { "type": ["string","null"] },
                  "label": { "type": ["string","null"] }
                }
              },
              "params": { "type": "object" },
              "rawInstruction": { "type": "string" },
              "needsClarification": { "type": "boolean" },
              "clarificationQuestion": { "type": ["string","null"] }
            }
          }
        }
      }
    }
    """;
}
