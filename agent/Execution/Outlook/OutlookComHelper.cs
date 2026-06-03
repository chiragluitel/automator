using System.Runtime.InteropServices;
using System.Text.Json;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Outlook;

/// <summary>
/// Cross-cutting utilities shared by all Outlook action handlers.
/// Covers: session acquisition, address parsing, body resolution,
/// attachment adding, COM item retrieval, and COM cleanup.
/// </summary>
public static class OutlookComHelper
{
    // ── Session management ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the existing OutlookSession for this run, or creates one on first use.
    /// All Outlook handlers call this — it is the single entry point for COM access.
    /// </summary>
    public static async Task<OutlookSession> GetOrCreateAsync(
        ExecutionContext ctx,
        ILogger? log = null)
    {
        if (ctx.Sessions.TryGetValue(OutlookSession.Key, out var s) && s is OutlookSession os)
            return os;

        log?.LogInformation("Creating Outlook COM session…");
        var session = await OutlookSession.CreateAsync(ctx.CancellationToken, log: log);
        ctx.Sessions[OutlookSession.Key] = session;
        return session;
    }

    // ── Recipient / address helpers ───────────────────────────────────────────

    /// <summary>
    /// Parses a recipient string that may be comma/semicolon-separated plain text
    /// or a JSON array of strings. Returns each trimmed non-empty address.
    /// </summary>
    public static IEnumerable<string> ParseAddresses(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Enumerable.Empty<string>();

        // Try JSON array first (e.g. params generated from a variable).
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.EnumerateArray()
                    .Select(e => e.GetString()?.Trim() ?? "")
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList();
        }
        catch { }

        return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(a => a.Trim())
                  .Where(a => !string.IsNullOrWhiteSpace(a));
    }

    /// <summary>
    /// Adds recipients of the given Outlook recipient type to a mail item.
    /// olTo = 1, olCC = 2, olBCC = 3.
    /// </summary>
    public static void AddRecipients(dynamic mail, string? raw, int recipientType = 1)
    {
        foreach (var addr in ParseAddresses(raw))
        {
            dynamic r = mail.Recipients.Add(addr);
            r.Type = recipientType;
        }
    }

    // ── Body helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the email body from either:
    ///   - params.html_body (sets HTMLBody — rich HTML email)
    ///   - params.body_variable (a ctx variable whose value is the body text)
    ///   - params.body (plain text)
    /// Returns (body, isHtml). Callers should set mail.HTMLBody when isHtml=true.
    /// </summary>
    public static (string Body, bool IsHtml) ResolveBody(IrStep step, ExecutionContext ctx)
    {
        var htmlBody = ParamOrNull(step, "html_body");
        if (htmlBody is not null)
            return (htmlBody, true);

        var bodyVar = ParamOrNull(step, "body_variable");
        if (bodyVar is not null && ctx.Variables.TryGetValue(bodyVar, out var varBody))
            return (varBody, false);

        return (ParamOrNull(step, "body") ?? "", false);
    }

    /// <summary>Applies a resolved body to a COM MailItem.</summary>
    public static void ApplyBody(dynamic mail, string body, bool isHtml)
    {
        if (isHtml)
            mail.HTMLBody = body;
        else
            mail.Body = body;
    }

    // ── Importance ────────────────────────────────────────────────────────────

    /// <summary>Maps "high"/"low"/"normal" → Outlook OlImportance int and applies it.</summary>
    public static void ApplyImportance(dynamic mail, string? raw)
    {
        if (raw is null) return;
        mail.Importance = raw.ToLowerInvariant() switch
        {
            "high" => 2,
            "low"  => 0,
            _      => 1
        };
    }

    // ── Attachment helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Attaches files to a mail item.
    /// Accepts comma/semicolon-separated paths, JSON arrays, or a single path.
    /// Each path is resolved via HandlerHelpers.ResolvePath (expands %ENV%, anchors to Downloads).
    /// </summary>
    public static void AddAttachments(dynamic mail, string? rawPaths)
    {
        if (string.IsNullOrWhiteSpace(rawPaths)) return;

        foreach (var rawPath in ParseAddresses(rawPaths)) // reuse address splitter
        {
            var resolved = Handlers.HandlerHelpers.ResolvePath(rawPath);
            if (!File.Exists(resolved))
                throw new FileNotFoundException(
                    $"Attachment not found: '{resolved}'. Check the file exists at that path.");
            mail.Attachments.Add(resolved);
        }
    }

    // ── COM item retrieval ────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves the first COM MailItem referenced by the named ctx variable.
    /// Caller is responsible for calling ReleaseComObject when done.
    /// </summary>
    public static dynamic GetFirstMailItem(OutlookSession session, ExecutionContext ctx, string variableName)
    {
        var json = GetVariableJson(ctx, variableName);
        var (entryId, storeId) = EmailItemSerializer.ExtractIdentity(json);
        return session.GetItemFromId(entryId, storeId);
    }

    /// <summary>
    /// Retrieves all COM MailItems referenced by the named ctx variable (supports arrays).
    /// Caller is responsible for calling ReleaseComObject on each when done.
    /// </summary>
    public static IReadOnlyList<dynamic> GetAllMailItems(OutlookSession session, ExecutionContext ctx, string variableName)
    {
        var json        = GetVariableJson(ctx, variableName);
        var identities  = EmailItemSerializer.ExtractAllIdentities(json);
        return identities.Select(id => session.GetItemFromId(id.EntryId, id.StoreId)).ToList();
    }

    // ── COM cleanup ───────────────────────────────────────────────────────────

    /// <summary>Safely releases a COM object, ignoring any errors.</summary>
    public static void Release(dynamic? obj)
    {
        if (obj is null) return;
        try { Marshal.ReleaseComObject(obj); } catch { }
    }

    /// <summary>Releases all items in a list of COM objects.</summary>
    public static void ReleaseAll(IEnumerable<dynamic> items)
    {
        foreach (var item in items) Release(item);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string GetVariableJson(ExecutionContext ctx, string variableName)
    {
        if (!ctx.Variables.TryGetValue(variableName, out var json))
            throw new InvalidOperationException(
                $"Variable '{variableName}' not found. " +
                $"Add a read_email step before this step and set params.variable to '{variableName}'.");
        return json;
    }

    private static string? ParamOrNull(IrStep step, string key) =>
        step.Params.TryGetValue(key, out var v) ? v?.ToString() : null;
}
