using AutoFlow.Agent;
using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Hub;
using Microsoft.Playwright;

// `dotnet run -- install` downloads Playwright browsers without needing PowerShell.
if (args.Length > 0 && args[0] == "install")
{
    Environment.Exit(Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }));
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddSingleton<IStepExecutor, PlaywrightExecutor>();
builder.Services.AddSingleton<AgentConnection>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
