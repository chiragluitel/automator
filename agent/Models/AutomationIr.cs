using System.Text.Json.Serialization;

namespace AutoFlow.Agent.Models;

// Minimal mirror of the server IR; only fields the executor needs.
public record AutomationIr
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("variables")] public List<IrVariable> Variables { get; init; } = new();
    [JsonPropertyName("steps")] public List<IrStep> Steps { get; init; } = new();
}

public record IrVariable
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("value")] public string? Value { get; init; }
}

public record IrStep
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("order")] public int Order { get; init; }
    [JsonPropertyName("action")] public string Action { get; init; } = "";
    [JsonPropertyName("target")] public IrTarget? Target { get; init; }
    [JsonPropertyName("params")] public Dictionary<string, object?> Params { get; init; } = new();
    [JsonPropertyName("rawInstruction")] public string RawInstruction { get; init; } = "";
}

public record IrTarget
{
    [JsonPropertyName("app")] public string? App { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("selector")] public string? Selector { get; init; }
    [JsonPropertyName("label")] public string? Label { get; init; }
    [JsonPropertyName("file")] public string? File { get; init; }
    [JsonPropertyName("sheet")] public string? Sheet { get; init; }
}

// Realtime payloads exchanged with the hub.
public record RunDispatchDto(
    Guid RunId,
    AutomationIr Definition,
    Dictionary<string, string>? InitialVariables = null
);
public record AgentStepReportDto(Guid RunId, string StepId, int StepOrder, string Status, string? Message);
public record AgentRunCompletedDto(Guid RunId, string Status, string? Error);

// Trigger payloads.
public record TriggerConfig(
    Guid TriggerId,
    Guid AutomationId,
    string Type,
    Dictionary<string, string> Conditions
);

public record TriggerFiredDto(
    Guid TriggerId,
    Guid AutomationId,
    Dictionary<string, string> InitialVariables
);

// Internal report passed from the executor back to the connection.
public record StepReport(string StepId, int StepOrder, string Status, string? Message);
