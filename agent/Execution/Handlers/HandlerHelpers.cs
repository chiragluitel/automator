using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

internal static class HandlerHelpers
{
    public static string Selector(IrStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.Target?.Selector)) return step.Target!.Selector!;
        if (!string.IsNullOrWhiteSpace(step.Target?.Label)) return $"text={step.Target!.Label}";
        throw new InvalidOperationException($"Step '{step.Id}' needs a target selector or label.");
    }

    public static string Param(IrStep step, string key) =>
        step.Params.TryGetValue(key, out var v) && v is not null
            ? v.ToString()!
            : throw new InvalidOperationException($"Step '{step.Id}' is missing params.{key}.");

    public static string? ParamOrNull(IrStep step, string key) =>
        step.Params.TryGetValue(key, out var v) ? v?.ToString() : null;
}
