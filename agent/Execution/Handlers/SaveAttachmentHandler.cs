using System.Runtime.InteropServices;
using System.Text.Json;
using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

/// <summary>
/// Saves email attachments to the local file system.
///
/// Supported params:
///   source_variable  — REQUIRED. Variable holding the email(s) whose attachments to save.
///   save_path        — Destination directory. Defaults to %USERPROFILE%\Downloads.
///                      Supports %ENV_VAR% expansion.
///   filename_filter  — Optional partial filename match (case-insensitive).
///                      Only attachments whose name contains this string are saved.
///   variable         — Optional. If set, stores a JSON array of saved file paths in this variable.
///   overwrite        — "false" to skip (not error) if the file already exists. Default: "true".
/// </summary>
public sealed class SaveAttachmentHandler : IActionHandler
{
    private readonly ILogger<SaveAttachmentHandler> _log;
    public string Action => "save_attachment";

    public SaveAttachmentHandler(ILogger<SaveAttachmentHandler> log) => _log = log;

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var sourceVar      = HandlerHelpers.Param(step, "source_variable");
        var rawSavePath    = HandlerHelpers.ParamOrNull(step, "save_path")
                             ?? "%USERPROFILE%\\Downloads";
        var filenameFilter = HandlerHelpers.ParamOrNull(step, "filename_filter");
        var outputVar      = HandlerHelpers.ParamOrNull(step, "variable");
        var overwrite      = !string.Equals(HandlerHelpers.ParamOrNull(step, "overwrite"), "false",
                                 StringComparison.OrdinalIgnoreCase);

        var saveDir = HandlerHelpers.ResolvePath(rawSavePath);
        Directory.CreateDirectory(saveDir);

        var session   = await OutlookComHelper.GetOrCreateAsync(ctx, _log);
        var mailItems = OutlookComHelper.GetAllMailItems(session, ctx, sourceVar);

        var savedPaths = new List<string>();

        foreach (var mail in mailItems)
        {
            try
            {
                int count = 0;
                try { count = (int)mail.Attachments.Count; } catch { }

                for (var i = 1; i <= count; i++)
                {
                    dynamic? att = null;
                    try
                    {
                        att = mail.Attachments[i];
                        var filename = "";
                        try { filename = (string)(att.FileName ?? att.DisplayName ?? $"attachment_{i}"); }
                        catch { filename = $"attachment_{i}"; }

                        if (filenameFilter is not null &&
                            !filename.Contains(filenameFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Sanitise filename for Windows.
                        var safe = SanitiseFilename(filename);
                        var dest = Path.Combine(saveDir, safe);

                        if (File.Exists(dest) && !overwrite)
                        {
                            _log.LogInformation("Skipping '{File}' — already exists and overwrite=false", dest);
                            savedPaths.Add(dest);
                            continue;
                        }

                        att.SaveAsFile(dest);
                        savedPaths.Add(dest);
                        _log.LogInformation("Saved attachment '{File}'", dest);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Failed to save attachment {Index}", i);
                    }
                    finally
                    {
                        OutlookComHelper.Release(att);
                    }
                }
            }
            finally
            {
                OutlookComHelper.Release(mail);
            }
        }

        if (savedPaths.Count == 0)
            _log.LogWarning("No attachments were saved. " +
                            "Check that the email has attachments and the filename_filter (if set) matches.");

        if (outputVar is not null)
        {
            ctx.Variables[outputVar] = JsonSerializer.Serialize(savedPaths);
            _log.LogInformation("Saved {Count} attachment(s); paths stored in '{Var}'",
                savedPaths.Count, outputVar);
        }
    }

    private static string SanitiseFilename(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
