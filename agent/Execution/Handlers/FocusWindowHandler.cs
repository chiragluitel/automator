using AutoFlow.Agent.Models;
using FlaUI.Core.Definitions;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class FocusWindowHandler : IActionHandler
{
    public string Action => "focus_window";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var title = step.Target?.Label
            ?? HandlerHelpers.ParamOrNull(step, "title")
            ?? throw new InvalidOperationException(
                $"Step '{step.Id}' needs target.label or params.title for focus_window.");

        var session = HandlerHelpers.GetOrCreateDesktopSession(ctx);
        var desktop = session.GetDesktop();

        // Search top-level children for a window whose name contains the title (case-insensitive).
        var allWindows = desktop.FindAllChildren(
            cf => cf.ByControlType(ControlType.Window));

        var window = allWindows.FirstOrDefault(
            w => w.Name.Contains(title, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No window with title containing '{title}' was found.");

        window.Focus();
        return Task.CompletedTask;
    }
}
