using AutoFlow.Application.Ir;

namespace AutoFlow.Application.Contracts;

// ---- Authoring input (what the React builder submits) --------------------

public record AuthoredStepDto(
    string RawInstruction,
    string? ScreenshotBase64,   // optional data (without data: prefix)
    string? ScreenshotMediaType // e.g. image/png
);

public record CompileRequestDto(
    string Name,
    string? Description,
    string? TriggerHint,                 // free text, e.g. "starts when email received"
    IReadOnlyList<AuthoredStepDto> Steps,
    IReadOnlyList<ClarificationAnswerDto>? Answers // present on re-submit
);

public record ClarificationAnswerDto(string StepId, string Answer);

// ---- Output ---------------------------------------------------------------

public record AutomationSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    int? CurrentVersionNumber,
    DateTimeOffset UpdatedAt
);

public record AutomationVersionDto(
    Guid Id,
    Guid AutomationId,
    int VersionNumber,
    string Status,
    AutomationIr Definition,
    DateTimeOffset CreatedAt
);

public record AutomationDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? CurrentVersionId,
    IReadOnlyList<AutomationVersionDto> Versions
);

// ---- Runs -----------------------------------------------------------------

public record RunStepLogDto(
    string StepId,
    int StepOrder,
    string Status,
    string? Message,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt
);

public record RunDto(
    Guid Id,
    Guid AutomationVersionId,
    string TriggerType,
    string Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<RunStepLogDto> StepLogs
);

// ---- Agent <-> server realtime payloads -----------------------------------

/// <summary>
/// Run dispatch payload sent from backend to agent.
/// InitialVariables are pre-seeded into ExecutionContext before step 1 runs —
/// used when a trigger fires (e.g. triggerEmail = the email JSON that matched).
/// </summary>
public record RunDispatchDto(
    Guid RunId,
    AutomationIr Definition,
    Dictionary<string, string>? InitialVariables = null
);

public record AgentStepReportDto(
    Guid RunId,
    string StepId,
    int StepOrder,
    string Status,        // running | succeeded | failed
    string? Message
);

public record AgentRunCompletedDto(Guid RunId, string Status, string? Error);

// ---- Trigger realtime payloads -------------------------------------------

/// <summary>Pushed by the backend to the agent on connect for each active non-manual trigger.</summary>
public record TriggerConfigDto(
    Guid TriggerId,
    Guid AutomationId,
    string Type,
    Dictionary<string, string> Conditions
);

/// <summary>Sent by the agent to the backend when a watched condition fires.</summary>
public record TriggerFiredDto(
    Guid TriggerId,
    Guid AutomationId,
    Dictionary<string, string> InitialVariables
);

// ---- Trigger REST DTOs ---------------------------------------------------

public record AutomationTriggerDto(
    Guid Id,
    Guid AutomationId,
    string Type,
    bool IsActive,
    Dictionary<string, string> Conditions
);
