using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Replies to an email that was previously stored in a ctx variable by read_email.
///
/// Supported params:
///   source_variable — REQUIRED. Name of the variable holding the email to reply to.
///   body            — Text to prepend before the original message quote.
///   html_body       — HTML body (takes precedence over body when both are set).
///   body_variable   — Name of a ctx variable whose value becomes the reply body.
///   reply_all       — "true" to reply to all recipients (default: "false").
///   attachments     — Comma-separated file paths or JSON array of paths to attach.
///   importance      — "high", "normal", "low".
/// </summary>
public sealed class ReplyEmailHandler : IActionHandler
{
    private readonly ILogger<ReplyEmailHandler> _log;
    public string Action => "reply_email";

    public ReplyEmailHandler(ILogger<ReplyEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var sourceVar = HandlerHelpers.Param(step, "source_variable");
        var replyAll  = string.Equals(HandlerHelpers.ParamOrNull(step, "reply_all"), "true",
                            StringComparison.OrdinalIgnoreCase);

        var session   = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        dynamic? original = null;
        dynamic? reply    = null;
        try
        {
            original = OutlookComHelper.GetFirstMailItem(session, ctx, sourceVar);
            reply    = replyAll ? original.ReplyAll() : original.Reply();

            var (body, isHtml) = OutlookComHelper.ResolveBody(step, ctx);

            if (!string.IsNullOrEmpty(body))
            {
                if (isHtml)
                {
                    // Prepend HTML before the quoted original HTML body.
                    reply.HTMLBody = body + "<br><br>" + (string)(reply.HTMLBody ?? "");
                }
                else
                {
                    reply.Body = body + Environment.NewLine + Environment.NewLine + (string)(reply.Body ?? "");
                }
            }

            OutlookComHelper.ApplyImportance(reply, HandlerHelpers.ParamOrNull(step, "importance"));
            OutlookComHelper.AddAttachments(reply, HandlerHelpers.ParamOrNull(step, "attachments"));

            reply.Send();
            _log.LogInformation("{Mode} sent for email in '{Var}'",
                replyAll ? "Reply-All" : "Reply", sourceVar);
        }
        finally
        {
            OutlookComHelper.Release(reply);
            OutlookComHelper.Release(original);
        }
    }
}
