using System.Text.RegularExpressions;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Variables;

public sealed class VariableResolver
{
    private static readonly Regex VarPattern =
        new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    // Returns a copy of the step with all string-valued params and target fields resolved.
    // If no variables are in scope the original step is returned unchanged (fast path).
    public IrStep Resolve(IrStep step, ExecutionContext ctx)
    {
        if (ctx.Variables.Count == 0) return step;

        var resolvedParams = step.Params.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value is string s ? (object?)Resolve(s, ctx) : kvp.Value);

        var t = step.Target;
        var resolvedTarget = t is null ? null : t with
        {
            Url = t.Url is null ? null : Resolve(t.Url, ctx),
            Selector = t.Selector is null ? null : Resolve(t.Selector, ctx),
            Label = t.Label is null ? null : Resolve(t.Label, ctx),
        };

        return step with { Params = resolvedParams, Target = resolvedTarget };
    }

    public string Resolve(string template, ExecutionContext ctx) =>
        VarPattern.Replace(template, m =>
            ctx.Variables.TryGetValue(m.Groups[1].Value, out var val) ? val : m.Value);
}
