using AutoFlow.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class SaveFileHandler : IActionHandler
{
    private readonly ILogger<SaveFileHandler> _log;

    public string Action => "save_file";

    public SaveFileHandler(ILogger<SaveFileHandler> log) => _log = log;

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var session = HandlerHelpers.GetOfficeSession(step, ctx);
        var rawPath = HandlerHelpers.ParamOrNull(step, "path");
        var savePath = rawPath is null ? null : HandlerHelpers.ResolvePath(rawPath);
        _log.LogInformation("Saving file{To}", savePath is null ? "" : $" to {savePath}");
        session.Save(savePath);
        return Task.CompletedTask;
    }
}
