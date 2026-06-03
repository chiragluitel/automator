using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Creates an email draft in Outlook without sending it.
/// The draft appears in the user's Drafts folder.
///
/// Supported params:
///   to             — Recipients (comma/semicolon or JSON array). Optional for a draft.
///   cc             — Optional CC.
///   bcc            — Optional BCC.
///   subject        — Email subject.
///   body           — Plain-text body.
///   html_body      — HTML body (takes precedence over body when both set).
///   body_variable  — Name of a ctx variable whose value becomes the body.
///   importance     — "high", "normal", "low".
///   attachments    — Comma-separated file paths or JSON array of paths.
///   variable       — Optional. If set, stores the draft's entryId in this variable
///                    so subsequent steps can locate and act on the draft.
/// </summary>
public sealed class CreateDraftHandler : IActionHandler
{
    private readonly ILogger<CreateDraftHandler> _log;
    public string Action => "create_draft";

    public CreateDraftHandler(ILogger<CreateDraftHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var outputVar = HandlerHelpers.ParamOrNull(step, "variable");

        var session = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        dynamic? draft = null;
        try
        {
            draft = session.Application.CreateItem(0); // 0 = olMailItem

            OutlookComHelper.AddRecipients(draft, HandlerHelpers.ParamOrNull(step, "to"),  1);
            OutlookComHelper.AddRecipients(draft, HandlerHelpers.ParamOrNull(step, "cc"),  2);
            OutlookComHelper.AddRecipients(draft, HandlerHelpers.ParamOrNull(step, "bcc"), 3);

            draft.Subject = HandlerHelpers.ParamOrNull(step, "subject") ?? "";

            var (body, isHtml) = OutlookComHelper.ResolveBody(step, ctx);
            OutlookComHelper.ApplyBody(draft, body, isHtml);
            OutlookComHelper.ApplyImportance(draft, HandlerHelpers.ParamOrNull(step, "importance"));
            OutlookComHelper.AddAttachments(draft, HandlerHelpers.ParamOrNull(step, "attachments"));

            // Save() writes to Drafts without sending.
            draft.Save();

            var entryId = "";
            try { entryId = (string)(draft.EntryID ?? ""); } catch { }

            if (outputVar is not null && !string.IsNullOrEmpty(entryId))
            {
                // Store a minimal email identity object so other handlers can find the draft.
                ctx.Variables[outputVar] = System.Text.Json.JsonSerializer.Serialize(
                    new { entryId, storeId = "", subject = HandlerHelpers.ParamOrNull(step, "subject") ?? "" });
            }

            _log.LogInformation("Draft saved to Drafts folder (entryId: {Id})", entryId);
        }
        finally
        {
            OutlookComHelper.Release(draft);
        }
    }
}
