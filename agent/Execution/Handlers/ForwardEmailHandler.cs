using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Forwards an email stored in a ctx variable to new recipients.
///
/// Supported params:
///   source_variable — REQUIRED. Variable holding the email to forward.
///   to              — REQUIRED. Forward-to recipients (comma/semicolon or JSON array).
///   cc              — Optional CC recipients.
///   body            — Text to prepend before the forwarded message.
///   html_body       — HTML body prepended (takes precedence over body when both set).
///   body_variable   — Name of a ctx variable whose value becomes the prepended body.
///   attachments     — Additional attachments to include (comma-separated paths or JSON array).
///   importance      — "high", "normal", "low".
/// </summary>
public sealed class ForwardEmailHandler : IActionHandler
{
    private readonly ILogger<ForwardEmailHandler> _log;
    public string Action => "forward_email";

    public ForwardEmailHandler(ILogger<ForwardEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var sourceVar = HandlerHelpers.Param(step, "source_variable");
        var to        = HandlerHelpers.Param(step, "to");

        var session  = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        dynamic? original = null;
        dynamic? fwd      = null;
        try
        {
            original = OutlookComHelper.GetFirstMailItem(session, ctx, sourceVar);
            fwd      = original.Forward();

            OutlookComHelper.AddRecipients(fwd, to, recipientType: 1);
            OutlookComHelper.AddRecipients(fwd, HandlerHelpers.ParamOrNull(step, "cc"), 2);

            if (!(bool)fwd.Recipients.ResolveAll())
                _log.LogWarning("One or more forward recipients could not be resolved in the address book.");

            var (body, isHtml) = OutlookComHelper.ResolveBody(step, ctx);
            if (!string.IsNullOrEmpty(body))
            {
                if (isHtml)
                    fwd.HTMLBody = body + "<br><br>" + (string)(fwd.HTMLBody ?? "");
                else
                    fwd.Body = body + Environment.NewLine + Environment.NewLine + (string)(fwd.Body ?? "");
            }

            OutlookComHelper.ApplyImportance(fwd, HandlerHelpers.ParamOrNull(step, "importance"));
            OutlookComHelper.AddAttachments(fwd, HandlerHelpers.ParamOrNull(step, "attachments"));

            fwd.Send();
            _log.LogInformation("Email forwarded to {To}", to);
        }
        finally
        {
            OutlookComHelper.Release(fwd);
            OutlookComHelper.Release(original);
        }
    }
}
