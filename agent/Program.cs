using AutoFlow.Agent;
using AutoFlow.Agent.Execution;
using AutoFlow.Agent.Hub;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddSingleton<IStepExecutor, PlaywrightExecutor>();
builder.Services.AddSingleton<AgentConnection>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
