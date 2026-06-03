using System.Diagnostics;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class OpenApplicationHandler : IActionHandler
{
    // Maps user-friendly names (case-insensitive) → canonical Windows exe name.
    private static readonly Dictionary<string, string> BuiltInAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["excel"]               = "EXCEL.EXE",
        ["microsoft excel"]     = "EXCEL.EXE",
        ["word"]                = "WINWORD.EXE",
        ["microsoft word"]      = "WINWORD.EXE",
        ["powerpoint"]          = "POWERPNT.EXE",
        ["microsoft powerpoint"]= "POWERPNT.EXE",
        ["outlook"]             = "OUTLOOK.EXE",
        ["microsoft outlook"]   = "OUTLOOK.EXE",
        ["notepad"]             = "notepad.exe",
        ["notepad++"]           = "notepad++.exe",
        ["paint"]               = "mspaint.exe",
        ["calculator"]          = "calc.exe",
        ["cmd"]                 = "cmd.exe",
        ["command prompt"]      = "cmd.exe",
        ["powershell"]          = "powershell.exe",
        ["file explorer"]       = "explorer.exe",
        ["explorer"]            = "explorer.exe",
    };

    // Office exe names that should also spin up an in-memory OfficeSession.
    private static readonly HashSet<string> OfficeExes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EXCEL.EXE", "WINWORD.EXE", "POWERPNT.EXE"
    };

    private readonly IEnumerable<ISessionFactory> _factories;
    private readonly ILogger<OpenApplicationHandler> _log;

    public string Action => "open_application";

    public OpenApplicationHandler(IEnumerable<ISessionFactory> factories, ILogger<OpenApplicationHandler> log)
    {
        _factories = factories;
        _log = log;
    }

    public async Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var app = step.Target?.App;

        // Browser: delegate to a registered session factory (WebSessionFactory).
        var factory = _factories.FirstOrDefault(f => f.CanCreate(app));
        if (factory is not null)
        {
            var key = factory.ResolveKey(app);
            if (!ctx.Sessions.ContainsKey(key))
                ctx.Sessions[key] = await factory.CreateAsync(ctx.CancellationToken);
            return;
        }

        if (app is null) return;

        // Resolve alias → exe name → full path via registry App Paths.
        var exeName = BuiltInAliases.TryGetValue(app, out var alias) ? alias
            : app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? app
            : app + ".exe";

        var fullPath = ResolveFromAppPaths(exeName);
        var launchTarget = fullPath ?? exeName;

        _log.LogInformation("Launching {App} → {Path}", app, launchTarget);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = launchTarget, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not launch '{app}' (resolved to '{launchTarget}'). " +
                $"Check the app is installed or supply its full exe path. Inner: {ex.Message}", ex);
        }

        // For Office apps, also create an in-memory ClosedXML workbook so that
        // set_cell / write_range / save_file steps have something to write to
        // without needing a separate open_file step.
        if (OfficeExes.Contains(exeName) && !ctx.Sessions.ContainsKey(OfficeSession.NewKey))
        {
            ctx.Sessions[OfficeSession.NewKey] = OfficeSession.CreateNew();
            _log.LogInformation("Created blank in-memory workbook for {App}", app);
        }
    }

    // Looks up the registered full path for an exe under HKLM App Paths.
    // Returns null if the key is not present or not running on Windows.
    private static string? ResolveFromAppPaths(string exeName)
    {
        if (!OperatingSystem.IsWindows()) return null;

        const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";

        // 64-bit registry first, then 32-bit view (for 32-bit Office on 64-bit Windows).
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key  = hklm.OpenSubKey(AppPathsKey + exeName);
            if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path))
                return path.Trim('"'); // registry values are sometimes quoted
        }

        return null;
    }
}
