using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Moves one or more emails (from a ctx variable) to a specified Outlook folder.
/// Supports moving a single email or a batch (when the variable holds a JSON array).
///
/// Supported params:
///   source_variable — REQUIRED. Variable holding the email(s) to move.
///   folder          — REQUIRED. Destination folder name or path (e.g. "Archive",
///                     "Projects/Q2", "Deleted Items").
/// </summary>
public sealed class MoveEmailHandler : IActionHandler
{
    private readonly ILogger<MoveEmailHandler> _log;
    public string Action => "move_email";

    public MoveEmailHandler(ILogger<MoveEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var sourceVar   = HandlerHelpers.Param(step, "source_variable");
        var folderName  = HandlerHelpers.Param(step, "folder");

        var session     = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        var items       = OutlookComHelper.GetAllMailItems(session, ctx, sourceVar);
        dynamic? dest   = null;

        try
        {
            dest = session.GetFolder(folderName);

            foreach (var mail in items)
            {
                try
                {
                    var subject = "";
                    try { subject = (string)(mail.Subject ?? ""); } catch { }
                    mail.Move(dest);
                    _log.LogInformation("Moved '{Subject}' → {Folder}", subject, folderName);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to move email to '{Folder}'", folderName);
                    throw;
                }
                finally
                {
                    OutlookComHelper.Release(mail);
                }
            }
        }
        finally
        {
            OutlookComHelper.Release(dest);
        }
    }
}
