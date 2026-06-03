using System.Diagnostics;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AutoFlow.Agent.Execution;

/// <summary>
/// Executes web-first IR actions with Playwright. Native desktop actions are the
/// next extension point (e.g. FlaUI / UI Automation); unsupported actions are
/// reported as skipped rather than failing the run.
/// </summary>
public class PlaywrightExecutor : IStepExecutor
{
    private readonly AgentOptions _opts;
    private readonly ILogger<PlaywrightExecutor> _log;

    public PlaywrightExecutor(IOptions<AgentOptions> opts, ILogger<PlaywrightExecutor> log)
    {
        _opts = opts.Value;
        _log = log;
    }

    public async Task<bool> ExecuteAsync(AutomationIr ir, Func<StepReport, Task> report, CancellationToken ct = default)
    {
        await using var session = new BrowserSession(_opts.Headless, _log);

        foreach (var step in ir.Steps.OrderBy(s => s.Order))
        {
            await report(new StepReport(step.Id, step.Order, "running", null));
            try
            {
                var skipped = await ExecuteStepAsync(session, step);
                await report(new StepReport(
                    step.Id, step.Order, "succeeded",
                    skipped ? $"Skipped — '{step.Action}' not yet supported by the agent." : null));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Step {StepId} ({Action}) failed", step.Id, step.Action);
                await report(new StepReport(step.Id, step.Order, "failed", ex.Message));
                return false;
            }
        }

        // In headed mode keep the browser open until the user closes it.
        if (!_opts.Headless)
        {
            _log.LogInformation("Automation complete — close the browser window to finish.");
            await session.WaitForBrowserCloseAsync();
        }

        return true;
    }

    /// <returns>true when the action was skipped (unsupported).</returns>
    private static async Task<bool> ExecuteStepAsync(BrowserSession session, IrStep step)
    {
        switch (step.Action)
        {
            case "open_application":
                await session.OpenApplicationAsync(step.Target?.App);
                return false;
            case "navigate":
                await session.Page.GotoAsync(Require(step.Target?.Url, "target.url"));
                return false;
            case "click":
                await session.Page.ClickAsync(Selector(step));
                return false;
            case "type_text":
                await session.Page.FillAsync(Selector(step), Param(step, "text"));
                return false;
            case "select_option":
                await session.Page.SelectOptionAsync(Selector(step), Param(step, "value"));
                return false;
            case "wait":
                await Task.Delay(WaitMs(step));
                return false;
            default:
                // read_email, extract, condition, loop, api_call — future work.
                return true;
        }
    }

    private static string Selector(IrStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.Target?.Selector)) return step.Target!.Selector!;
        if (!string.IsNullOrWhiteSpace(step.Target?.Label)) return $"text={step.Target!.Label}";
        throw new InvalidOperationException($"Step '{step.Id}' needs a target selector or label.");
    }

    private static string Param(IrStep step, string key)
    {
        if (step.Params.TryGetValue(key, out var value) && value is not null)
            return Substitute(value.ToString() ?? "");
        throw new InvalidOperationException($"Step '{step.Id}' is missing params.{key}.");
    }

    // Variable resolution ({{name}}) is not implemented in the MVP; values pass through.
    private static string Substitute(string text) => text;

    private static int WaitMs(IrStep step) =>
        step.Params.TryGetValue("ms", out var v) && int.TryParse(v?.ToString(), out var ms) ? ms : 1000;

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Missing required {name}.")
            : value;
}

/// <summary>Owns the Playwright browser/page lifecycle for a single run.</summary>
internal sealed class BrowserSession : IAsyncDisposable
{
    private static readonly HashSet<string> Browsers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "google chrome", "chromium", "edge", "msedge", "microsoft edge",
            "firefox", "browser", "safari", "webkit"
        };

    private readonly bool _headless;
    private readonly ILogger _log;

    private IPlaywright? _pw;
    private IBrowser? _browser;
    private IPage? _page;

    public BrowserSession(bool headless, ILogger log)
    {
        _headless = headless;
        _log = log;
    }

    public IPage Page =>
        _page ?? throw new InvalidOperationException("No browser page open. Add an 'open application' step first.");

    public async Task OpenApplicationAsync(string? app)
    {
        if (app is null || Browsers.Contains(app))
        {
            _pw ??= await Playwright.CreateAsync();
            _browser ??= await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = _headless });
            _page ??= await _browser.NewPageAsync();
            return;
        }

        // Native application — best-effort launch (extend with UI Automation later).
        _log.LogInformation("Launching native application {App}", app);
        Process.Start(new ProcessStartInfo { FileName = app, UseShellExecute = true });
    }

    public Task WaitForBrowserCloseAsync()
    {
        if (_page is null) return Task.CompletedTask;
        var tcs = new TaskCompletionSource();
        _page.Close += (_, _) => tcs.TrySetResult();
        return tcs.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _pw?.Dispose();
    }
}
