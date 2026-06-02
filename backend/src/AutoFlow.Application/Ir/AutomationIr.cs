using System.Text.Json.Serialization;

namespace AutoFlow.Application.Ir;

/// <summary>
/// Strongly-typed mirror of contracts/automation-ir.schema.json.
/// This is the canonical shape Claude emits and the agent consumes.
/// </summary>
public record AutomationIr
{
    [JsonPropertyName("name")] public string Name { get; init; } = default!;
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("trigger")] public IrTrigger Trigger { get; init; } = new();
    [JsonPropertyName("variables")] public List<IrVariable> Variables { get; init; } = new();
    [JsonPropertyName("steps")] public List<IrStep> Steps { get; init; } = new();
}

public record IrTrigger
{
    [JsonPropertyName("type")] public string Type { get; init; } = "manual";
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("schedule")] public string? Schedule { get; init; }
    [JsonPropertyName("conditions")] public List<IrCondition> Conditions { get; init; } = new();
}

public record IrCondition
{
    [JsonPropertyName("field")] public string Field { get; init; } = default!;
    [JsonPropertyName("op")] public string Op { get; init; } = "equals";
    [JsonPropertyName("value")] public object? Value { get; init; }
}

public record IrVariable
{
    [JsonPropertyName("name")] public string Name { get; init; } = default!;
    [JsonPropertyName("type")] public string Type { get; init; } = "string";
    [JsonPropertyName("from")] public string? From { get; init; }
    [JsonPropertyName("value")] public object? Value { get; init; }
}

public record IrStep
{
    [JsonPropertyName("id")] public string Id { get; init; } = default!;
    [JsonPropertyName("order")] public int Order { get; init; }
    [JsonPropertyName("action")] public string Action { get; init; } = default!;
    [JsonPropertyName("target")] public IrTarget? Target { get; init; }
    [JsonPropertyName("params")] public Dictionary<string, object?> Params { get; init; } = new();
    [JsonPropertyName("rawInstruction")] public string RawInstruction { get; init; } = default!;
    [JsonPropertyName("assetRefs")] public List<string> AssetRefs { get; init; } = new();
    [JsonPropertyName("needsClarification")] public bool NeedsClarification { get; init; }
    [JsonPropertyName("clarificationQuestion")] public string? ClarificationQuestion { get; init; }
}

public record IrTarget
{
    [JsonPropertyName("app")] public string? App { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("selector")] public string? Selector { get; init; }
    [JsonPropertyName("label")] public string? Label { get; init; }
}
