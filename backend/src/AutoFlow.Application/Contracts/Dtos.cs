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

public record RunDispatchDto(Guid RunId, AutomationIr Definition);

public record AgentStepReportDto(
    Guid RunId,
    string StepId,
    int StepOrder,
    string Status,        // running | succeeded | failed
    string? Message
);

public record AgentRunCompletedDto(Guid RunId, string Status, string? Error);
