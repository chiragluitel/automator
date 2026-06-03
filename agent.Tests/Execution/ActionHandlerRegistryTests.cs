using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Execution.Handlers;
using AutoFlow.Agent.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace AutoFlow.Agent.Tests.Execution;

public class ActionHandlerRegistryTests
{
    [Fact]
    public void TryGetHandler_RegisteredAction_ReturnsHandler()
    {
        var handler = MakeHandler("click");
        var registry = new ActionHandlerRegistry(new[] { handler });

        var found = registry.TryGetHandler("click", out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(handler);
    }

    [Fact]
    public void TryGetHandler_LookupIsCaseInsensitive()
    {
        var handler = MakeHandler("type_text");
        var registry = new ActionHandlerRegistry(new[] { handler });

        registry.TryGetHandler("TYPE_TEXT", out var result).Should().BeTrue();
        result.Should().BeSameAs(handler);
    }

    [Fact]
    public void TryGetHandler_UnknownAction_ReturnsFalse()
    {
        var registry = new ActionHandlerRegistry(Enumerable.Empty<IActionHandler>());

        registry.TryGetHandler("navigate", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_DuplicateAction_Throws()
    {
        var handlers = new[] { MakeHandler("click"), MakeHandler("click") };

        var act = () => new ActionHandlerRegistry(handlers);

        act.Should().Throw<ArgumentException>();
    }

    private static IActionHandler MakeHandler(string action)
    {
        var mock = new Mock<IActionHandler>();
        mock.SetupGet(h => h.Action).Returns(action);
        return mock.Object;
    }
}
