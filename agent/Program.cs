using AutoFlow.Agent;
using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Execution.Handlers;
using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Execution.Triggers;
using AutoFlow.Agent.Execution.Variables;
using AutoFlow.Agent.Hub;

// `dotnet run -- install` downloads Playwright browsers without needing PowerShell.
if (args.Length > 0 && args[0] == "install")
{
    Environment.Exit(Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }));
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

// ── Session factories ────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISessionFactory, WebSessionFactory>();

// ── Web handlers ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IActionHandler, OpenApplicationHandler>();
builder.Services.AddSingleton<IActionHandler, NavigateHandler>();
builder.Services.AddSingleton<IActionHandler, ClickHandler>();
builder.Services.AddSingleton<IActionHandler, TypeTextHandler>();
builder.Services.AddSingleton<IActionHandler, SelectOptionHandler>();
builder.Services.AddSingleton<IActionHandler, WaitHandler>();
builder.Services.AddSingleton<IActionHandler, ExtractHandler>();

// ── Outlook / email handlers ──────────────────────────────────────────────────
builder.Services.AddSingleton<IActionHandler, ReadEmailHandler>();
builder.Services.AddSingleton<IActionHandler, SendEmailHandler>();
builder.Services.AddSingleton<IActionHandler, ReplyEmailHandler>();
builder.Services.AddSingleton<IActionHandler, ForwardEmailHandler>();
builder.Services.AddSingleton<IActionHandler, MoveEmailHandler>();
builder.Services.AddSingleton<IActionHandler, DeleteEmailHandler>();
builder.Services.AddSingleton<IActionHandler, MarkEmailHandler>();
builder.Services.AddSingleton<IActionHandler, SaveAttachmentHandler>();
builder.Services.AddSingleton<IActionHandler, CreateDraftHandler>();

// ── Excel / file handlers ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IActionHandler, OpenFileHandler>();
builder.Services.AddSingleton<IActionHandler, SaveFileHandler>();
builder.Services.AddSingleton<IActionHandler, ReadCellHandler>();
builder.Services.AddSingleton<IActionHandler, ReadRangeHandler>();
builder.Services.AddSingleton<IActionHandler, SetCellHandler>();
builder.Services.AddSingleton<IActionHandler, WriteRangeHandler>();

// ── Desktop / UIA handlers ────────────────────────────────────────────────────
builder.Services.AddSingleton<IActionHandler, PressKeysHandler>();
builder.Services.AddSingleton<IActionHandler, FocusWindowHandler>();

// ── Core execution pipeline ───────────────────────────────────────────────────
builder.Services.AddSingleton<ActionHandlerRegistry>();
builder.Services.AddSingleton<VariableResolver>();
builder.Services.AddSingleton<IStepExecutor, RunExecutor>();

// ── Trigger watchers ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<TriggerWatcherManager>();

builder.Services.AddSingleton<AgentConnection>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
