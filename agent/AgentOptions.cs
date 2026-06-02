namespace AutoFlow.Agent;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string BackendUrl { get; set; } = "http://localhost:8080";
    public string HubPath { get; set; } = "/hubs/agent";
    public string Token { get; set; } = "dev-agent-token";
    public string UserEmail { get; set; } = "demo@amcor.com";
    public string MachineName { get; set; } = Environment.MachineName;

    /// <summary>Run the browser headless. Default false so the user can watch it work.</summary>
    public bool Headless { get; set; } = false;
}
