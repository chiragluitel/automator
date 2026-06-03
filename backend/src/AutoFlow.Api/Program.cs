using AutoFlow.Api.Middleware;
using AutoFlow.Api.Realtime;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Services;
using AutoFlow.Infrastructure;

// Load .env for local `dotnet run`. Docker-compose injects vars directly so this is a no-op there.
LoadDotEnv();

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

static void LoadDotEnv()
{
    // Walk up from the runtime binary to find the repo-root .env file.
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".env")))
        dir = dir.Parent;
    if (dir is null) return;

    foreach (var line in File.ReadAllLines(Path.Combine(dir.FullName, ".env")))
    {
        if (line.StartsWith('#') || !line.Contains('=')) continue;
        var idx = line.IndexOf('=');
        var k = line[..idx].Trim();
        var v = line[(idx + 1)..].Trim();
        // Don't overwrite vars already set in the environment (docker-compose takes priority).
        if (Environment.GetEnvironmentVariable(k) is null)
            Environment.SetEnvironmentVariable(k, v);
    }

    // Bridge naming: .env uses ANTHROPIC_API_KEY; ASP.NET Core reads Anthropic__ApiKey.
    if (Environment.GetEnvironmentVariable("Anthropic__ApiKey") is null)
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (key is not null) Environment.SetEnvironmentVariable("Anthropic__ApiKey", key);
    }
}
