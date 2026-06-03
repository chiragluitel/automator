using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoFlow.Agent.Execution.Sessions;

public sealed class WebSessionFactory : ISessionFactory
{
    private static readonly HashSet<string> BrowserNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "google chrome", "chromium", "edge", "msedge", "microsoft edge",
            "firefox", "browser", "safari", "webkit",
        };

    private readonly bool _headless;
    private readonly ILogger<WebSession> _log;

    public WebSessionFactory(IOptions<AgentOptions> opts, ILogger<WebSession> log)
    {
        _headless = opts.Value.Headless;
        _log = log;
    }

    public bool CanCreate(string? app) => app is null || BrowserNames.Contains(app);

    public string ResolveKey(string? app) => WebSession.Key;

    public async Task<ISession> CreateAsync(CancellationToken ct = default) =>
        await WebSession.CreateAsync(_headless, _log, ct);
}
