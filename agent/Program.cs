using AutoFlow.Agent;
using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Execution.Handlers;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Execution.Variables;
using AutoFlow.Agent.Hub;

// `dotnet run -- install` downloads Playwright browsers without needing PowerShell.
if (args.Length > 0 && args[0] == "install")
{
    Environment.Exit(Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }));
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

// Sessions
builder.Services.AddSingleton<ISessionFactory, WebSessionFactory>();

// Handlers — each registered as IActionHandler; ActionHandlerRegistry picks them all up.
builder.Services.AddSingleton<IActionHandler, OpenApplicationHandler>();
builder.Services.AddSingleton<IActionHandler, NavigateHandler>();
builder.Services.AddSingleton<IActionHandler, ClickHandler>();
builder.Services.AddSingleton<IActionHandler, TypeTextHandler>();
builder.Services.AddSingleton<IActionHandler, SelectOptionHandler>();
builder.Services.AddSingleton<IActionHandler, WaitHandler>();
builder.Services.AddSingleton<IActionHandler, ExtractHandler>();

// Core execution
builder.Services.AddSingleton<ActionHandlerRegistry>();
builder.Services.AddSingleton<VariableResolver>();
builder.Services.AddSingleton<IStepExecutor, RunExecutor>();

builder.Services.AddSingleton<AgentConnection>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
