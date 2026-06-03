using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution;

public interface IStepExecutor
{
    /// <summary>
    /// Executes the IR step-by-step, invoking <paramref name="report"/> with progress.
    /// <paramref name="initialVariables"/> are seeded into ExecutionContext before step 1
    /// (used when a trigger fires and provides e.g. triggerEmail).
    /// Returns true if every step succeeded.
    /// </summary>
    Task<bool> ExecuteAsync(
        Guid runId,
        AutomationIr ir,
        Func<StepReport, Task> report,
        Dictionary<string, string>? initialVariables,
        CancellationToken ct = default);
}
