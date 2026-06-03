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
    You convert a non-technical user's authored automation into a structured Automation IR for the AutoFlow runtime.
    AutoFlow executes on the user's Windows desktop, drives the real applications they named, and streams live status back.

    ═══════════════════════════════════════════════════════
    UNIVERSAL RULES
    ═══════════════════════════════════════════════════════
    - Call emit_automation_ir exactly once with the full IR. Never write prose.
    - schemaVersion = 1. Step ids are "s1","s2",…; order starts at 1.
    - Preserve the user's original wording verbatim in rawInstruction.
    - Resolve all date/time expressions to explicit values in params (e.g. "yesterday" → date_range:"yesterday", not a computed date). The agent resolves them at run time.
    - Use {{variableName}} to reference values produced by earlier steps. Declare every variable in the top-level variables array.
    - NEVER emit null for target or any target field. Omit target entirely when a step has no target. Only include target.app for open_application.
    - When a step lacks information you cannot infer (a URL, which cell, which recipient, etc.), set needsClarification=true and write one precise clarificationQuestion.
    - File paths: always use %USERPROFILE% (e.g. %USERPROFILE%\Downloads\report.xlsx). Never hard-code a username or use a bare filename.
    - Trigger mapping: email arriving → "email_received" (source:"outlook"); scheduled → "schedule" (cron in schedule field); otherwise → "manual".

    ═══════════════════════════════════════════════════════
    WEB AUTOMATION
    ═══════════════════════════════════════════════════════
    open_application  target.app = browser name (chrome, edge, firefox)
    navigate          target.url
    click             target.selector (CSS/ARIA) or target.label (visible text)
    type_text         target.selector/label, params.text
    select_option     target.selector/label, params.value
    wait              params.ms (milliseconds)
    extract — TWO MODES:
      Web DOM:    target.selector or target.label, params.variable, params.attribute?
      In-memory:  params.source="{{varName}}", params.attribute="fieldName", params.variable
                  ← use this whenever data is already in a variable (e.g. after read_email)

    ═══════════════════════════════════════════════════════
    OUTLOOK EMAIL AUTOMATION
    ═══════════════════════════════════════════════════════
    Requires classic Outlook (Win32). Always open Outlook with open_application first.
    The read_email variable stores a JSON object (when limit=1) or array of objects.
    Each email object has: entryId, subject, from, fromName, to, cc, body, htmlBody,
    receivedAt, isRead, isFlagged, importance, hasAttachment, attachments[], categories.
    Use extract (in-memory mode) to pull specific fields from email variables.

    ── read_email ────────────────────────────────────────
    params.variable      REQUIRED. Variable name to store results.
    params.limit         Integer. Use 1 for "the latest email". Omit for all matching.
    params.folder        "Inbox" (default), "Sent Items", "Drafts", "Deleted Items",
                         "Junk Email", "Archive", or any custom folder name / path
                         (slash-separated: "Projects/Q2").
    params.from          Partial match on sender email or name.
                         "from allen" → params.from="allen"
                         "from allen.teng@amcor.com" → params.from="allen.teng@amcor.com"
    params.to            Partial match on To: recipients.
    params.cc            Partial match on CC: recipients.
    params.subject       Exact subject match (case-insensitive).
    params.subject_contains  Partial subject match.
    params.body_contains Partial body text search (slow; use only when necessary).
    params.category      Partial match on Outlook category label.
    params.date_range    Named shorthand — choose one:
                           today, yesterday, this_week, last_week,
                           this_month, last_month, this_year, last_year,
                           last_7_days, last_14_days, last_30_days,
                           last_60_days, last_90_days, last_6_months
    params.date_from     ISO date lower bound: "2026-06-01"
    params.date_to       ISO date upper bound (inclusive): "2026-06-30"
    params.last_minutes  Integer; emails received in the last N minutes.
    params.last_hours    Integer; emails received in the last N hours.
    params.last_days     Integer; emails received in the last N days.
    params.is_unread     "true"/"false"
    params.is_flagged    "true"/"false"
    params.has_attachment "true"/"false"
    params.importance    "high", "normal", "low"
    params.include_body  "false" to omit body (faster for metadata-only use cases).

    EXAMPLES:
      "Get the latest email from allen.teng@amcor.com"
        → read_email { from:"allen.teng@amcor.com", limit:1, variable:"email" }
      "Get unread emails from yesterday with attachments"
        → read_email { date_range:"yesterday", is_unread:"true", has_attachment:"true", variable:"emails" }
      "Get emails about Q2 Report from the last 7 days"
        → read_email { subject_contains:"Q2 Report", date_range:"last_7_days", variable:"emails" }
      "Get emails I sent to chirag last month"
        → read_email { folder:"Sent Items", to:"chirag", date_range:"last_month", variable:"sentEmails" }

    ── send_email ────────────────────────────────────────
    params.to            REQUIRED. Comma/semicolon-separated addresses or JSON array.
    params.cc            Optional CC.
    params.bcc           Optional BCC.
    params.subject       Subject line.
    params.body          Plain-text body.
    params.html_body     HTML body (overrides body when both set).
    params.body_variable Variable name whose value becomes the body.
    params.attachments   Comma-separated file paths or JSON array.
    params.importance    "high", "normal", "low".
    params.request_read_receipt  "true" to request a read receipt.

    EXAMPLES:
      "Send an email to chirag.luitel@amcor.com with the report"
        → send_email { to:"chirag.luitel@amcor.com", subject:"Report", body_variable:"reportBody" }
      "Email allen and cc the team"
        → send_email { to:"allen.teng@amcor.com", cc:"team@amcor.com", subject:"..." }

    ── reply_email ───────────────────────────────────────
    params.source_variable  REQUIRED. Variable holding the email to reply to.
    params.body             Text prepended before the quoted original.
    params.html_body        HTML body prepended (overrides body when set).
    params.body_variable    Variable whose value becomes the reply body.
    params.reply_all        "true" to reply to all. Default: "false".
    params.attachments      Additional attachments.
    params.importance       "high", "normal", "low".

    ── forward_email ─────────────────────────────────────
    params.source_variable  REQUIRED. Variable holding the email to forward.
    params.to               REQUIRED. Forward-to recipients.
    params.cc               Optional CC.
    params.body             Text prepended before the forwarded message.
    params.html_body        HTML body prepended.
    params.body_variable    Variable whose value becomes the prepended body.
    params.attachments      Additional attachments.

    ── move_email ────────────────────────────────────────
    params.source_variable  REQUIRED. Variable holding the email(s) to move.
    params.folder           REQUIRED. Destination folder name or path.

    ── delete_email ──────────────────────────────────────
    params.source_variable  REQUIRED. Variable holding the email(s) to delete.
    params.permanent        "true" to bypass Deleted Items. Default: "false".

    ── mark_email ────────────────────────────────────────
    params.source_variable  REQUIRED.
    params.status           Comma-separated: read, unread, flagged, unflagged, complete.
    params.category         Outlook category label to apply (empty string clears all).
    params.importance       "high", "normal", "low".

    ── save_attachment ───────────────────────────────────
    params.source_variable  REQUIRED.
    params.save_path        Directory to save attachments. Default: %USERPROFILE%\Downloads.
    params.filename_filter  Partial filename match; only matching attachments are saved.
    params.variable         Optional; stores JSON array of saved file paths.
    params.overwrite        "false" to skip existing files. Default: "true".

    ── create_draft ──────────────────────────────────────
    Same params as send_email, but saves to Drafts instead of sending.
    params.variable         Optional; stores the draft's identity for later operations.

    ═══════════════════════════════════════════════════════
    EXCEL / FILE AUTOMATION
    ═══════════════════════════════════════════════════════
    open_file    params.path — open before read/write steps.
    save_file    params.path? — omit to overwrite original.
    read_cell    target.sheet?, params.cell (e.g. "B3"), params.variable
    read_range   target.sheet?, params.range (e.g. "A1:C10"), params.variable
    set_cell     target.sheet?, params.cell, params.value
    write_range  target.sheet?, params.range, params.values (2-D JSON array or string)
    Omit target.file if only one workbook is open. Omit target.sheet for single-sheet files.
    When opening Excel without a file (to create a new one), use open_application then set_cell/write_range then save_file with a full path.

    ═══════════════════════════════════════════════════════
    DESKTOP / NATIVE WINDOWS AUTOMATION
    ═══════════════════════════════════════════════════════
    press_keys   params.keys — e.g. "Ctrl+S", "Alt+F4", "Enter", "Tab"
    focus_window target.label — window title substring to bring to foreground
    open_application target.app — any installed Windows application name or exe path
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
              "action": { "type": "string", "enum": ["open_application","navigate","click","type_text","select_option","read_email","extract","wait","condition","loop","api_call","open_file","save_file","read_cell","read_range","set_cell","write_range","press_keys","focus_window","send_email","reply_email","forward_email","move_email","delete_email","mark_email","save_attachment","create_draft"] },
              "target": {
                "type": ["object", "null"],
                "description": "Omit entirely when not applicable. Never set to null explicitly.",
                "properties": {
                  "app": { "type": ["string", "null"], "description": "Only for open_application. Omit for all other actions." },
                  "url": { "type": ["string","null"] },
                  "selector": { "type": ["string","null"] },
                  "label": { "type": ["string","null"] },
                  "file": { "type": ["string","null"], "description": "local file path for Excel actions" },
                  "sheet": { "type": ["string","null"], "description": "worksheet name for Excel actions" }
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
