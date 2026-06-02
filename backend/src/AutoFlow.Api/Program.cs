using AutoFlow.Api.Middleware;
using AutoFlow.Api.Realtime;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Services;
using AutoFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Infrastructure (EF, repositories, Claude, MinIO, validation).
builder.Services.AddInfrastructure(builder.Configuration);

// Application use-case services.
builder.Services.AddScoped<IAutomationService, AutomationService>();
builder.Services.AddScoped<IRunService, RunService>();

// Realtime + identity.
builder.Services.AddSingleton<IAgentConnectionTracker, InMemoryAgentConnectionTracker>();
builder.Services.AddSingleton<IAgentDispatcher, SignalRAgentDispatcher>();
builder.Services.AddSingleton<ICurrentUser, DemoCurrentUser>();

// CORS — permissive for the MVP internal tool; tighten to known origins later.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

app.MapControllers();
app.MapHub<AgentHub>("/hubs/agent");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Ensure the object-storage bucket exists on startup.
using (var scope = app.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<IAssetStorage>();
    try { await storage.EnsureBucketAsync(); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Could not ensure MinIO bucket on startup."); }
}

app.Run();
