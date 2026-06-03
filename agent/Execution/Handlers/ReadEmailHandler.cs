using System.Runtime.InteropServices;
using System.Text.Json;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Reads emails from classic Outlook via COM.
/// Requires classic Outlook (Win32) — new Outlook (web-based) has no COM Object Model.
/// </summary>
public sealed class ReadEmailHandler : IActionHandler
{
    private readonly ILogger<ReadEmailHandler> _log;

    public string Action => "read_email";

    public ReadEmailHandler(ILogger<ReadEmailHandler> log) => _log = log;

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var folder   = HandlerHelpers.ParamOrNull(step, "folder") ?? "Inbox";
        var variable = HandlerHelpers.ParamOrNull(step, "variable") ?? "emails"; // graceful default
        var mins     = int.TryParse(HandlerHelpers.ParamOrNull(step, "last_minutes"), out var m) ? m : 30;
        var limit    = int.TryParse(HandlerHelpers.ParamOrNull(step, "limit"), out var l) ? l : int.MaxValue;

        _log.LogInformation("Reading emails from {Folder} (last {Mins} min, limit {Limit})", folder, mins, limit);
        var emails = ReadViaOutlookCom(folder, mins, limit);
        ctx.Variables[variable] = JsonSerializer.Serialize(emails);
        _log.LogInformation("Stored {Count} email(s) in '{Variable}'", emails.Count, variable);
        return Task.CompletedTask;
    }

    private static List<Dictionary<string, string>> ReadViaOutlookCom(string folderName, int lastMinutes, int limit)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("ReadEmailHandler requires Windows.");

        var t = Type.GetTypeFromProgID("Outlook.Application")
            ?? throw new InvalidOperationException(
                "Outlook is not installed or not registered. Install classic Outlook (the Win32 app) and try again.");

        dynamic outlook = Activator.CreateInstance(t)!;
        try
        {
            dynamic ns = outlook.GetNamespace("MAPI");
            dynamic folder = ResolveFolder(ns, folderName);

            // Outlook Restrict filter uses locale-aware date format; en-US slash format is safest.
            var cutoff = DateTime.Now.AddMinutes(-lastMinutes);
            var filter = $"[ReceivedTime] >= '{cutoff:MM/dd/yyyy HH:mm}'";

            dynamic items = folder.Items;
            items.Sort("[ReceivedTime]", true); // newest first
            dynamic restricted = items.Restrict(filter);

            var result = new List<Dictionary<string, string>>();
            foreach (dynamic mail in restricted)
            {
                if (result.Count >= limit)
                {
                    try { Marshal.ReleaseComObject(mail); } catch { }
                    break;
                }
                try
                {
                    result.Add(new Dictionary<string, string>
                    {
                        ["subject"]    = (string)(mail.Subject    ?? ""),
                        ["from"]       = (string)(mail.SenderName ?? ""),
                        ["body"]       = (string)(mail.Body       ?? ""),
                        ["receivedAt"] = ((DateTime)mail.ReceivedTime).ToString("o"),
                    });
                }
                catch
                {
                    // Skip non-mail items (meeting requests, read receipts, etc.)
                }
                finally
                {
                    try { if (mail is not null) Marshal.ReleaseComObject(mail); } catch { }
                }
            }

            try { Marshal.ReleaseComObject(restricted); } catch { }
            try { Marshal.ReleaseComObject(items); } catch { }
            try { Marshal.ReleaseComObject(folder); } catch { }
            try { Marshal.ReleaseComObject(ns); } catch { }
            return result;
        }
        finally
        {
            try { Marshal.ReleaseComObject(outlook); } catch { }
        }
    }

    private static dynamic ResolveFolder(dynamic ns, string name) =>
        name.ToLowerInvariant() switch
        {
            "inbox"                    => ns.GetDefaultFolder(6),  // olFolderInbox
            "sent" or "sent items"     => ns.GetDefaultFolder(5),  // olFolderSentMail
            "drafts"                   => ns.GetDefaultFolder(16), // olFolderDrafts
            "deleted" or "trash"       => ns.GetDefaultFolder(3),  // olFolderDeletedItems
            "outbox"                   => ns.GetDefaultFolder(4),  // olFolderOutbox
            _                          => ns.GetDefaultFolder(6),  // default: Inbox
        };
}
