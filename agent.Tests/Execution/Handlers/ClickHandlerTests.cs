using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Execution.Handlers;
using ExecutionContext = AutoFlow.Agent.Execution.ExecutionContext;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;
using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace AutoFlow.Agent.Tests.Execution.Handlers;

public class ClickHandlerTests
{
    private readonly ClickHandler _handler = new();

    [Fact]
    public async Task ExecuteAsync_UsesSelector_WhenPresent()
    {
        var (ctx, page) = SetupCtx();
        page.Setup(p => p.ClickAsync("#submit", It.IsAny<PageClickOptions?>()))
            .Returns(Task.CompletedTask);

        await _handler.ExecuteAsync(
            new IrStep { Id = "s1", Action = "click", Target = new IrTarget { Selector = "#submit" } },
            ctx);

        page.Verify(p => p.ClickAsync("#submit", It.IsAny<PageClickOptions?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToTextLabel_WhenNoSelector()
    {
        var (ctx, page) = SetupCtx();
        page.Setup(p => p.ClickAsync("text=Submit", It.IsAny<PageClickOptions?>()))
            .Returns(Task.CompletedTask);

        await _handler.ExecuteAsync(
            new IrStep { Id = "s1", Action = "click", Target = new IrTarget { Label = "Submit" } },
            ctx);

        page.Verify(p => p.ClickAsync("text=Submit", It.IsAny<PageClickOptions?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenNoSelectorOrLabel()
    {
        var (ctx, _) = SetupCtx();
        var step = new IrStep { Id = "s1", Action = "click", Target = new IrTarget() };

        await _handler.Invoking(h => h.ExecuteAsync(step, ctx))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*selector or label*");
    }

    private static (ExecutionContext ctx, Mock<IPage> page) SetupCtx()
    {
        var page = new Mock<IPage>();
        var session = new Mock<IWebSession>();
        session.SetupGet(s => s.Page).Returns(page.Object);
        var ctx = new ExecutionContext(Guid.NewGuid());
        ctx.Sessions[WebSession.Key] = session.Object;
        return (ctx, page);
    }
}
