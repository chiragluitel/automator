using AutoFlow.Application.Contracts;
using AutoFlow.Application.Ir;
using AutoFlow.Domain.Entities;

namespace AutoFlow.Application.Abstractions;

/// <summary>Persistence port for automations and their versions/runs.</summary>
public interface IAutomationRepository
{
    Task<Automation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Automation?> GetWithVersionsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Automation>> ListAsync(Guid ownerId, CancellationToken ct = default);
    Task AddAsync(Automation automation, CancellationToken ct = default);

    Task<int> NextVersionNumberAsync(Guid automationId, CancellationToken ct = default);
    Task AddVersionAsync(AutomationVersion version, CancellationToken ct = default);
    Task<AutomationVersion?> GetVersionAsync(Guid versionId, CancellationToken ct = default);
    Task AddAssetAsync(AutomationAsset asset, CancellationToken ct = default);

    Task AddRunAsync(AutomationRun run, CancellationToken ct = default);
    Task<AutomationRun?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task AddOrUpdateStepLogAsync(RunStepLog log, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Turns authored steps + screenshots into a validated IR using Claude.</summary>
public interface ICompilationService
{
    Task<AutomationIr> CompileAsync(CompileRequestDto request, CancellationToken ct = default);
}

/// <summary>Validates an IR document against the JSON Schema contract.</summary>
public interface IIrValidator
{
    /// <returns>List of human-readable errors; empty when valid.</returns>
    IReadOnlyList<string> Validate(string irJson);
}

/// <summary>Object storage for authoring screenshots and run evidence.</summary>
public interface IAssetStorage
{
    Task<string> PutAsync(byte[] data, string contentType, CancellationToken ct = default);
    Task EnsureBucketAsync(CancellationToken ct = default);
}

/// <summary>Tracks which agent connection belongs to which user (in-memory MVP).</summary>
public interface IAgentConnectionTracker
{
    void Register(string userEmail, string connectionId);
    void Remove(string connectionId);
    string? GetConnectionForUser(string userEmail);
}

/// <summary>Pushes a run to a connected agent over the realtime channel.</summary>
public interface IAgentDispatcher
{
    Task<bool> DispatchAsync(string userEmail, RunDispatchDto dispatch, CancellationToken ct = default);
}

/// <summary>Use-case service for authoring and reading automations.</summary>
public interface IAutomationService
{
    Task<IReadOnlyList<AutomationSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<AutomationDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AutomationVersionDto> CompileAndSaveAsync(Guid? automationId, CompileRequestDto request, CancellationToken ct = default);
    Task<AutomationVersionDto> ActivateAsync(Guid automationId, Guid versionId, CancellationToken ct = default);
}

/// <summary>Use-case service for triggering and recording runs.</summary>
public interface IRunService
{
    Task<RunDto> TriggerAsync(Guid automationId, string triggerType, CancellationToken ct = default);
    Task<RunDto?> GetAsync(Guid runId, CancellationToken ct = default);
    Task RecordStepAsync(AgentStepReportDto report, CancellationToken ct = default);
    Task CompleteAsync(AgentRunCompletedDto completed, CancellationToken ct = default);
}
