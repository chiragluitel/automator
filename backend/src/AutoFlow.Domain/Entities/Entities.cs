using AutoFlow.Domain.Enums;

namespace AutoFlow.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Automation> Automations { get; set; } = new List<Automation>();
}

public class Automation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? Owner { get; set; }
    public AutomationVersion? CurrentVersion { get; set; }
    public ICollection<AutomationVersion> Versions { get; set; } = new List<AutomationVersion>();
}

public class AutomationVersion
{
    public Guid Id { get; set; }
    public Guid AutomationId { get; set; }
    public int VersionNumber { get; set; }

    /// <summary>The IR, stored as JSONB. Serialized JSON matching automation-ir.schema.json.</summary>
    public string Definition { get; set; } = default!;

    public VersionStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Automation? Automation { get; set; }
    public ICollection<AutomationAsset> Assets { get; set; } = new List<AutomationAsset>();
}

public class AutomationAsset
{
    public Guid Id { get; set; }
    public Guid AutomationVersionId { get; set; }
    public string? StepId { get; set; }
    public string ObjectKey { get; set; } = default!;
    public string ContentType { get; set; } = "image/png";
    public DateTimeOffset CreatedAt { get; set; }
}

public class Agent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string MachineName { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public string? AppVersion { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AutomationRun
{
    public Guid Id { get; set; }
    public Guid AutomationVersionId { get; set; }
    public Guid? AgentId { get; set; }
    public string TriggerType { get; set; } = "manual";
    public RunStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AutomationVersion? AutomationVersion { get; set; }
    public ICollection<RunStepLog> StepLogs { get; set; } = new List<RunStepLog>();
}

public class RunStepLog
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string StepId { get; set; } = default!;
    public int StepOrder { get; set; }
    public RunStatus Status { get; set; }
    public string? Message { get; set; }
    public string? ScreenshotObjectKey { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>
/// Persists a non-manual trigger watcher config for an automation.
/// One row per automation (upserted when the version is activated).
/// Conditions is a flat JSON dict matching read_email param names so the
/// agent can evaluate them without re-parsing the full IR.
/// </summary>
public class AutomationTrigger
{
    public Guid Id { get; set; }
    public Guid AutomationId { get; set; }
    public string Type { get; set; } = "email_received";
    public bool IsActive { get; set; } = true;
    /// <summary>JSONB — flat key/value map, e.g. {"from":"sd@amcor.com","subject_contains":"Access"}</summary>
    public string Conditions { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Automation? Automation { get; set; }
}
