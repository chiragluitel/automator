using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public interface IActionHandler
{
    string Action { get; }
    Task ExecuteAsync(IrStep step, ExecutionContext ctx);
}
