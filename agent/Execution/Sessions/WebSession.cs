using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace AutoFlow.Agent.Execution.Sessions;

public sealed class WebSession : IWebSession
{
    public const string Key = "web";

    private readonly bool _headless;
    private readonly ILogger _log;
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private IPage? _page;

    private WebSession(bool headless, ILogger log)
    {
        _headless = headless;
        _log = log;
    }

    public static async Task<WebSession> CreateAsync(bool headless, ILogger log, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var session = new WebSession(headless, log);
        session._pw = await Playwright.CreateAsync();
        session._browser = await session._pw.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = headless });
        session._page = await session._browser.NewPageAsync();
        return session;
    }

    public IPage Page =>
        _page ?? throw new InvalidOperationException("WebSession not initialized.");

    public Task WaitForCloseIfNeededAsync(CancellationToken ct)
    {
        if (_headless || _page is null) return Task.CompletedTask;

        _log.LogInformation("Automation complete — close the browser window to finish.");
        var tcs = new TaskCompletionSource();
        _page.Close += (_, _) => tcs.TrySetResult();
        ct.Register(() => tcs.TrySetCanceled());
        return tcs.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _pw?.Dispose();
    }
}
