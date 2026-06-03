using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Composes and sends a new email via classic Outlook COM.
///
/// Supported params:
///   to             — REQUIRED. Comma/semicolon-separated recipient addresses or JSON array.
///   cc             — Optional CC recipients.
///   bcc            — Optional BCC recipients.
///   subject        — Email subject line.
///   body           — Plain-text body.
///   html_body      — HTML body (takes precedence over body when both are set).
///   body_variable  — Name of a ctx variable whose value becomes the body.
///   importance     — "high", "normal" (default), "low".
///   attachments    — Comma-separated file paths or JSON array of paths.
///   request_read_receipt — "true" to request a read receipt.
///   save_to_sent   — "false" to not save a copy in Sent Items (default: true).
/// </summary>
public sealed class SendEmailHandler : IActionHandler
{
    private readonly ILogger<SendEmailHandler> _log;
    public string Action => "send_email";

    public SendEmailHandler(ILogger<SendEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var to = HandlerHelpers.Param(step, "to");

        var session = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        dynamic? mail = null;
        try
        {
            mail = session.Application.CreateItem(0); // 0 = olMailItem

            OutlookComHelper.AddRecipients(mail, to, recipientType: 1);       // To
            OutlookComHelper.AddRecipients(mail, HandlerHelpers.ParamOrNull(step, "cc"),  2); // CC
            OutlookComHelper.AddRecipients(mail, HandlerHelpers.ParamOrNull(step, "bcc"), 3); // BCC

            if (!(bool)mail.Recipients.ResolveAll())
                _log.LogWarning("One or more recipients could not be resolved in the address book. " +
                                "The email will be sent but may fail delivery for unresolved addresses.");

            mail.Subject = HandlerHelpers.ParamOrNull(step, "subject") ?? "";

            var (body, isHtml) = OutlookComHelper.ResolveBody(step, ctx);
            OutlookComHelper.ApplyBody(mail, body, isHtml);
            OutlookComHelper.ApplyImportance(mail, HandlerHelpers.ParamOrNull(step, "importance"));
            OutlookComHelper.AddAttachments(mail, HandlerHelpers.ParamOrNull(step, "attachments"));

            if (string.Equals(HandlerHelpers.ParamOrNull(step, "request_read_receipt"), "true",
                    StringComparison.OrdinalIgnoreCase))
                mail.ReadReceiptRequested = true;

            if (string.Equals(HandlerHelpers.ParamOrNull(step, "save_to_sent"), "false",
                    StringComparison.OrdinalIgnoreCase))
                mail.DeleteAfterSubmit = true;

            mail.Send();
            _log.LogInformation("Email sent to {To}", to);
        }
        finally
        {
            OutlookComHelper.Release(mail);
        }
    }
}
