using Microsoft.Playwright;

namespace AutoFlow.Agent.Execution.Sessions;

// Thin browser-surface interface. Exists so web handlers are testable
// without a real browser — mock IPage in unit tests.
public interface IWebSession : ISession
{
    IPage Page { get; }
}
