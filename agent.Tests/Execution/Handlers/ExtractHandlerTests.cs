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

public class ExtractHandlerTests
{
    private readonly ExtractHandler _handler = new();

    [Fact]
    public async Task ExecuteAsync_StoresInnerText_InContextVariables()
    {
        var (ctx, page) = SetupCtx();
        page.Setup(p => p.InnerTextAsync("#price", It.IsAny<PageInnerTextOptions?>()))
            .ReturnsAsync("  $42.00  ");

        await _handler.ExecuteAsync(
            StepWith("#price", variable: "price"),
            ctx);

        ctx.Variables["price"].Should().Be("$42.00");
    }

    [Fact]
    public async Task ExecuteAsync_StoresAttribute_WhenAttributeParamProvided()
    {
        var (ctx, page) = SetupCtx();
        page.Setup(p => p.GetAttributeAsync("a#link", "href", It.IsAny<PageGetAttributeOptions?>()))
            .ReturnsAsync("https://example.com");

        await _handler.ExecuteAsync(
            StepWith("a#link", variable: "url", attribute: "href"),
            ctx);

        ctx.Variables["url"].Should().Be("https://example.com");
    }

    [Fact]
    public async Task ExecuteAsync_StoresEmptyString_WhenAttributeReturnsNull()
    {
        var (ctx, page) = SetupCtx();
        page.Setup(p => p.GetAttributeAsync("img", "alt", It.IsAny<PageGetAttributeOptions?>()))
            .ReturnsAsync((string?)null);

        await _handler.ExecuteAsync(
            StepWith("img", variable: "altText", attribute: "alt"),
            ctx);

        ctx.Variables["altText"].Should().Be("");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenVariableParamMissing()
    {
        var (ctx, _) = SetupCtx();
        var step = new IrStep
        {
            Id = "s1", Action = "extract",
            Target = new IrTarget { Selector = "#val" },
        };

        await _handler.Invoking(h => h.ExecuteAsync(step, ctx))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*params.variable*");
    }

    private static IrStep StepWith(string selector, string variable, string? attribute = null)
    {
        var p = new Dictionary<string, object?> { ["variable"] = variable };
        if (attribute is not null) p["attribute"] = attribute;
        return new IrStep
        {
            Id = "s1", Action = "extract",
            Target = new IrTarget { Selector = selector },
            Params = p,
        };
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
