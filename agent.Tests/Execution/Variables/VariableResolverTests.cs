using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Execution.Variables;
using ExecutionContext = AutoFlow.Agent.Execution.ExecutionContext;
using AutoFlow.Agent.Models;
using FluentAssertions;
using Xunit;

namespace AutoFlow.Agent.Tests.Execution.Variables;

public class VariableResolverTests
{
    private readonly VariableResolver _resolver = new();

    [Fact]
    public void Resolve_String_SubstitutesKnownVariable()
    {
        var ctx = CtxWith(("name", "Alice"));

        _resolver.Resolve("Hello {{name}}", ctx).Should().Be("Hello Alice");
    }

    [Fact]
    public void Resolve_String_LeavesUnknownVariableUnchanged()
    {
        var ctx = new ExecutionContext(Guid.NewGuid());

        _resolver.Resolve("{{unknown}}", ctx).Should().Be("{{unknown}}");
    }

    [Fact]
    public void Resolve_String_ReplacesMultipleVariables()
    {
        var ctx = CtxWith(("first", "John"), ("last", "Doe"));

        _resolver.Resolve("{{first}} {{last}}", ctx).Should().Be("John Doe");
    }

    [Fact]
    public void Resolve_Step_FastPathWhenNoVariables()
    {
        var ctx = new ExecutionContext(Guid.NewGuid());
        var step = new IrStep { Id = "s1", Action = "click",
            Params = new Dictionary<string, object?> { ["text"] = "{{value}}" } };

        var result = _resolver.Resolve(step, ctx);

        result.Should().BeSameAs(step); // identical reference — no copy made
    }

    [Fact]
    public void Resolve_Step_SubstitutesParamValues()
    {
        var ctx = CtxWith(("email", "user@example.com"));
        var step = new IrStep
        {
            Id = "s1", Action = "type_text",
            Target = new IrTarget { Selector = "#email" },
            Params = new Dictionary<string, object?> { ["text"] = "{{email}}" },
        };

        var result = _resolver.Resolve(step, ctx);

        result.Params["text"].Should().Be("user@example.com");
    }

    [Fact]
    public void Resolve_Step_SubstitutesTargetUrl()
    {
        var ctx = CtxWith(("host", "example.com"));
        var step = new IrStep
        {
            Id = "s1", Action = "navigate",
            Target = new IrTarget { Url = "https://{{host}}/login" },
        };

        var result = _resolver.Resolve(step, ctx);

        result.Target!.Url.Should().Be("https://example.com/login");
    }

    [Fact]
    public void Resolve_Step_NonStringParamValuesPassThrough()
    {
        var ctx = CtxWith(("x", "1"));
        var step = new IrStep
        {
            Id = "s1", Action = "wait",
            Params = new Dictionary<string, object?> { ["ms"] = 500 },
        };

        var result = _resolver.Resolve(step, ctx);

        result.Params["ms"].Should().Be(500);
    }

    private static ExecutionContext CtxWith(params (string key, string value)[] variables)
    {
        var ctx = new ExecutionContext(Guid.NewGuid());
        foreach (var (key, value) in variables)
            ctx.Variables[key] = value;
        return ctx;
    }
}
