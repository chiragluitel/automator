using System.Diagnostics;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class OpenApplicationHandler : IActionHandler
{
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

        var factory = _factories.FirstOrDefault(f => f.CanCreate(app));
        if (factory is not null)
        {
            var key = factory.ResolveKey(app);
            if (!ctx.Sessions.ContainsKey(key))
                ctx.Sessions[key] = await factory.CreateAsync(ctx.CancellationToken);
            return;
        }

        // Native app — best-effort launch; session tracking arrives in Phase 3.
        if (app is not null)
        {
            _log.LogInformation("Launching native application {App}", app);
            Process.Start(new ProcessStartInfo { FileName = app, UseShellExecute = true });
        }
    }
}
