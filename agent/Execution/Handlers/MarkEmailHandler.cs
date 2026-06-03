using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Changes the read/flag/category status of one or more emails stored in a ctx variable.
///
/// Supported params:
///   source_variable — REQUIRED. Variable holding the email(s) to update.
///   status          — One or more of the following (comma-separated for multiple):
///                       "read"        — mark as read.
///                       "unread"      — mark as unread.
///                       "flagged"     — set follow-up flag.
///                       "unflagged"   — clear follow-up flag.
///                       "complete"    — mark follow-up flag as complete.
///   category        — Outlook category label to apply (e.g. "Red Category", "Work").
///                     Set to "" to clear all categories.
///   importance      — "high", "normal", "low" — change the importance level.
/// </summary>
public sealed class MarkEmailHandler : IActionHandler
{
    private readonly ILogger<MarkEmailHandler> _log;
    public string Action => "mark_email";

    public MarkEmailHandler(ILogger<MarkEmailHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var sourceVar  = HandlerHelpers.Param(step, "source_variable");
        var status     = HandlerHelpers.ParamOrNull(step, "status");
        var category   = HandlerHelpers.ParamOrNull(step, "category");
        var importance = HandlerHelpers.ParamOrNull(step, "importance");

        if (status is null && category is null && importance is null)
            throw new InvalidOperationException(
                "mark_email requires at least one of: params.status, params.category, params.importance.");

        var session = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        var items   = OutlookComHelper.GetAllMailItems(session, ctx, sourceVar);

        foreach (var mail in items)
        {
            try
            {
                var subject = "";
                try { subject = (string)(mail.Subject ?? ""); } catch { }

                ApplyStatus(mail, status);
                ApplyCategory(mail, category);
                OutlookComHelper.ApplyImportance(mail, importance);

                mail.Save();
                _log.LogInformation("Marked '{Subject}': status={S} category={C} importance={I}",
                    subject, status ?? "-", category ?? "-", importance ?? "-");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to mark email");
                throw;
            }
            finally
            {
                OutlookComHelper.Release(mail);
            }
        }
    }

    private static void ApplyStatus(dynamic mail, string? status)
    {
        if (status is null) return;

        foreach (var token in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "read":
                    mail.UnRead = false;
                    break;
                case "unread":
                    mail.UnRead = true;
                    break;
                case "flagged":
                    mail.FlagStatus = 2; // olFlagMarked
                    break;
                case "unflagged":
                case "cleared":
                    mail.FlagStatus = 0; // olNoFlag
                    break;
                case "complete":
                    mail.FlagStatus = 1; // olFlagComplete
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown mark status '{token}'. " +
                        $"Valid values: read, unread, flagged, unflagged, complete.");
            }
        }
    }

    private static void ApplyCategory(dynamic mail, string? category)
    {
        if (category is null) return;
        mail.Categories = category; // Empty string clears; multiple = comma-separated.
    }
}
