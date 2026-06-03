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

public class TypeTextHandlerTests
{
    private readonly TypeTextHandler _handler = new();

    [Fact]
    public async Task ExecuteAsync_FillsCorrectSelectorAndText()
    {
        var (ctx, page) = SetupCtx();
        page.Setup(p => p.FillAsync("#email", "user@example.com", It.IsAny<PageFillOptions?>()))
            .Returns(Task.CompletedTask);

        await _handler.ExecuteAsync(
            new IrStep
            {
                Id = "s1", Action = "type_text",
                Target = new IrTarget { Selector = "#email" },
                Params = new Dictionary<string, object?> { ["text"] = "user@example.com" },
            },
            ctx);

        page.Verify(p => p.FillAsync("#email", "user@example.com", It.IsAny<PageFillOptions?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenTextParamMissing()
    {
        var (ctx, _) = SetupCtx();
        var step = new IrStep
        {
            Id = "s1", Action = "type_text",
            Target = new IrTarget { Selector = "#email" },
        };

        await _handler.Invoking(h => h.ExecuteAsync(step, ctx))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*params.text*");
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
