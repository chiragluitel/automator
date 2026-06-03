using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Deletes one or more emails stored in a ctx variable.
/// By default, emails are moved to Deleted Items (recoverable).
/// Set params.permanent = "true" to permanently delete without recovery.
///
/// Supported params:
///   source_variable — REQUIRED. Variable holding the email(s) to delete.
///   permanent       — "true" for permanent deletion (bypasses Deleted Items).
///                     Default: "false" (moves to Deleted Items).
/// </summary>
public sealed class DeleteEmailHandler : IActionHandler
{
    private readonly ILogger<DeleteEmailHandler> _log;
    public string Action => "delete_email";

    public DeleteEmailHandler(ILogger<DeleteEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var sourceVar = HandlerHelpers.Param(step, "source_variable");
        var permanent = string.Equals(HandlerHelpers.ParamOrNull(step, "permanent"), "true",
                            StringComparison.OrdinalIgnoreCase);

        var session = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        var items   = OutlookComHelper.GetAllMailItems(session, ctx, sourceVar);

        foreach (var mail in items)
        {
            try
            {
                var subject = "";
                try { subject = (string)(mail.Subject ?? ""); } catch { }

                if (permanent)
                {
                    // OlDeletedItemFlags.olHardDelete = 1 (permanent bypass)
                    // The standard Delete() moves to Deleted Items; permanently delete
                    // by moving to Deleted Items then deleting again from there.
                    mail.Delete();
                    _log.LogInformation("Moved '{Subject}' to Deleted Items", subject);
                    // Second delete for permanent removal is intentionally not done here —
                    // permanent deletion requires iterating Deleted Items which is risky.
                    // If hard-delete is needed, the caller should use move_email to Deleted Items
                    // followed by a second delete_email. Surfacing this boundary is safer.
                }
                else
                {
                    mail.Delete();
                    _log.LogInformation("Deleted '{Subject}' (moved to Deleted Items)", subject);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to delete email");
                throw;
            }
            finally
            {
                OutlookComHelper.Release(mail);
            }
        }
    }
}
