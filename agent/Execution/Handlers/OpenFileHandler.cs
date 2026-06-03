using AutoFlow.Agent.Execution.Sessions;
using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class OpenFileHandler : IActionHandler
{
    private readonly ILogger<OpenFileHandler> _log;

    public string Action => "open_file";

    public OpenFileHandler(ILogger<OpenFileHandler> log) => _log = log;

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var path = HandlerHelpers.Param(step, "path");
        var fullPath = HandlerHelpers.ResolvePath(path);

        if (ctx.Sessions.ContainsKey(fullPath)) return Task.CompletedTask; // already open

        _log.LogInformation("Opening file {Path}", fullPath);
        ctx.Sessions[fullPath] = new OfficeSession(fullPath);
        return Task.CompletedTask;
    }
}
