using AutoFlow.Agent.Execution.Outlook;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

internal static class HandlerHelpers
{
    public static string Selector(IrStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.Target?.Selector)) return step.Target!.Selector!;
        if (!string.IsNullOrWhiteSpace(step.Target?.Label)) return $"text={step.Target!.Label}";
        throw new InvalidOperationException($"Step '{step.Id}' needs a target selector or label.");
    }

    public static string Param(IrStep step, string key) =>
        step.Params.TryGetValue(key, out var v) && v is not null
            ? v.ToString()!
            : throw new InvalidOperationException($"Step '{step.Id}' is missing params.{key}.");

    public static string? ParamOrNull(IrStep step, string key) =>
        step.Params.TryGetValue(key, out var v) ? v?.ToString() : null;

    // Resolves a user-supplied file path:
    //   1. Expands %ENV_VAR% tokens and leading ~
    //   2. If still relative, anchors to the user's Downloads folder
    public static string ResolvePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);

        if (expanded.StartsWith("~"))
            expanded = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                       + expanded[1..];

        if (!Path.IsPathRooted(expanded))
        {
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            expanded = Path.Combine(downloads, expanded);
        }

        return Path.GetFullPath(expanded);
    }

    // Finds the OfficeSession for a step.
    // Uses target.file (resolved to full path) when present; otherwise falls back to the sole open OfficeSession.
    public static OfficeSession GetOfficeSession(IrStep step, ExecutionContext ctx)
    {
        var file = step.Target?.File;
        if (file is not null)
        {
            var key = ResolvePath(file);
            if (ctx.Sessions.TryGetValue(key, out var s) && s is OfficeSession os)
                return os;
            throw new InvalidOperationException(
                $"File '{file}' is not open. Add an 'open_file' step first.");
        }

        return ctx.Sessions.Values.OfType<OfficeSession>().SingleOrDefault()
            ?? throw new InvalidOperationException(
                "No file is open. Add an 'open_file' step first.");
    }

    // Returns the existing OutlookSession or creates one lazily (async).
    // Exposed here so handlers that already use HandlerHelpers don't need an extra using.
    public static Task<OutlookSession> GetOrCreateOutlookSessionAsync(ExecutionContext ctx,
        Microsoft.Extensions.Logging.ILogger? log = null) =>
        OutlookComHelper.GetOrCreateAsync(ctx, log);

    // Returns the existing DesktopSession or creates one lazily.
    public static DesktopSession GetOrCreateDesktopSession(ExecutionContext ctx)
    {
        if (ctx.Sessions.TryGetValue(DesktopSession.Key, out var s) && s is DesktopSession ds)
            return ds;
        ds = new DesktopSession();
        ctx.Sessions[DesktopSession.Key] = ds;
        return ds;
    }
}
