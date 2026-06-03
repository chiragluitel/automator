using AutoFlow.Agent.Execution.Handlers;
using AutoFlow.Agent.Execution.Variables;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution;

public sealed class RunExecutor : IStepExecutor
{
    private readonly ActionHandlerRegistry _registry;
    private readonly VariableResolver _resolver;
    private readonly ILogger<RunExecutor> _log;

    public RunExecutor(ActionHandlerRegistry registry, VariableResolver resolver, ILogger<RunExecutor> log)
    {
        _registry = registry;
        _resolver = resolver;
        _log = log;
    }

    public async Task<bool> ExecuteAsync(
        Guid runId,
        AutomationIr ir,
        Func<StepReport, Task> report,
        Dictionary<string, string>? initialVariables,
        CancellationToken ct = default)
    {
        var ctx = new ExecutionContext(runId, ct);

        // Seed any pre-defined variable values declared in the IR.
        foreach (var v in ir.Variables.Where(v => v.Value is not null))
            ctx.Variables[v.Name] = v.Value!;

        // Overlay trigger-provided runtime variables (e.g. triggerEmail from an email trigger).
        // These take precedence over IR defaults since they are live runtime values.
        if (initialVariables is not null)
            foreach (var kv in initialVariables)
                ctx.Variables[kv.Key] = kv.Value;

        try
        {
            foreach (var step in ir.Steps.OrderBy(s => s.Order))
            {
                ct.ThrowIfCancellationRequested();
                await report(new StepReport(step.Id, step.Order, "running", null));

                try
                {
                    var resolved = _resolver.Resolve(step, ctx);

                    if (_registry.TryGetHandler(step.Action, out var handler))
                    {
                        await handler.ExecuteAsync(resolved, ctx);
                        await report(new StepReport(step.Id, step.Order, "succeeded", null));
                    }
                    else
                    {
                        await report(new StepReport(step.Id, step.Order, "succeeded",
                            $"Skipped — '{step.Action}' not yet supported by the agent."));
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Step {StepId} ({Action}) failed", step.Id, step.Action);
                    await report(new StepReport(step.Id, step.Order, "failed", ex.Message));
                    return false;
                }
            }

            // Headed mode: let each session wait for the user to close it.
            foreach (var session in ctx.Sessions.Values)
            {
                try { await session.WaitForCloseIfNeededAsync(ct); }
                catch (OperationCanceledException) { break; }
            }

            return true;
        }
        finally
        {
            foreach (var session in ctx.Sessions.Values)
                await session.DisposeAsync();
        }
    }
}
