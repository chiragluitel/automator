using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution;

public interface IStepExecutor
{
    /// <summary>
    /// Executes the IR step-by-step, invoking <paramref name="report"/> with progress.
    /// Returns true if every step succeeded.
    /// </summary>
    Task<bool> ExecuteAsync(AutomationIr ir, Func<StepReport, Task> report, CancellationToken ct = default);
}
