using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace AutoFlow.Agent.Execution.Sessions;

/// <summary>
/// Wraps a FlaUI UIA3 automation instance for the lifetime of a run.
/// Lazily created on the first press_keys or focus_window step.
/// </summary>
public sealed class DesktopSession : ISession
{
    public const string Key = "desktop";

    private readonly UIA3Automation _automation;

    public DesktopSession()
    {
        _automation = new UIA3Automation();
    }

    public AutomationElement GetDesktop() => _automation.GetDesktop();

    public Task WaitForCloseIfNeededAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _automation.Dispose();
        return ValueTask.CompletedTask;
    }
}
