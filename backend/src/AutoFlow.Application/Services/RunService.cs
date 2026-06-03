using System.Text.Json;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using AutoFlow.Application.Ir;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;

namespace AutoFlow.Application.Services;

public class RunService : IRunService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAutomationRepository _repo;
    private readonly IAgentDispatcher _dispatcher;
    private readonly ICurrentUser _user;

    public RunService(IAutomationRepository repo, IAgentDispatcher dispatcher, ICurrentUser user)
    {
        _repo = repo;
        _dispatcher = dispatcher;
        _user = user;
    }

    public async Task<RunDto> TriggerAsync(Guid automationId, string triggerType, CancellationToken ct = default)
    {
        var automation = await _repo.GetByIdAsync(automationId, ct)
            ?? throw new KeyNotFoundException($"Automation {automationId} not found");

        if (automation.CurrentVersionId is not { } versionId)
            throw new InvalidOperationException("Automation has no current version.");

        var version = await _repo.GetVersionAsync(versionId, ct)
            ?? throw new InvalidOperationException("Current version missing.");

        if (version.Status != VersionStatus.Active)
            throw new InvalidOperationException("Only active versions can be run. Activate the version first.");

        var run = new AutomationRun
        {
            Id = Guid.NewGuid(),
            AutomationVersionId = version.Id,
            TriggerType = triggerType,
            Status = RunStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repo.AddRunAsync(run, ct);
        await _repo.SaveChangesAsync(ct);

        var ir = JsonSerializer.Deserialize<AutomationIr>(version.Definition, Json)!;
        var dispatched = await _dispatcher.DispatchAsync(_user.Email, new RunDispatchDto(run.Id, ir), ct);

        if (dispatched)
        {
            run.Status = RunStatus.Dispatched;
        }
        else
        {
            run.Status = RunStatus.Failed;
            run.Error = "No agent is currently connected for this user.";
            run.FinishedAt = DateTimeOffset.UtcNow;
        }
        await _repo.SaveChangesAsync(ct);

        return await GetAsync(run.Id, ct) ?? throw new InvalidOperationException("Run vanished after creation.");
    }

    public async Task<RunDto> TriggerByAgentAsync(
        Guid automationId,
        string userEmail,
        Dictionary<string, string>? initialVariables,
        CancellationToken ct = default)
    {
        var automation = await _repo.GetByIdAsync(automationId, ct)
            ?? throw new KeyNotFoundException($"Automation {automationId} not found");

        if (automation.CurrentVersionId is not { } versionId)
            throw new InvalidOperationException("Automation has no current version.");

        var version = await _repo.GetVersionAsync(versionId, ct)
            ?? throw new InvalidOperationException("Current version missing.");

        if (version.Status != VersionStatus.Active)
            throw new InvalidOperationException("Only active versions can be run.");

        var run = new AutomationRun
        {
            Id = Guid.NewGuid(),
            AutomationVersionId = version.Id,
            TriggerType = "email_received",
            Status = RunStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repo.AddRunAsync(run, ct);
        await _repo.SaveChangesAsync(ct);

        var ir = JsonSerializer.Deserialize<AutomationIr>(version.Definition, Json)!;
        var dispatched = await _dispatcher.DispatchAsync(userEmail, new RunDispatchDto(run.Id, ir, initialVariables), ct);

        if (dispatched)
        {
            run.Status = RunStatus.Dispatched;
        }
        else
        {
            run.Status = RunStatus.Failed;
            run.Error = "Agent disconnected before dispatch could complete.";
            run.FinishedAt = DateTimeOffset.UtcNow;
        }
        await _repo.SaveChangesAsync(ct);

        return await GetAsync(run.Id, ct) ?? throw new InvalidOperationException("Run missing after creation.");
    }

    public async Task<RunDto?> GetAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _repo.GetRunAsync(runId, ct);
        if (run is null) return null;

        var logs = run.StepLogs
            .OrderBy(l => l.StepOrder)
            .Select(l => new RunStepLogDto(
                l.StepId, l.StepOrder, l.Status.ToString(), l.Message, l.StartedAt, l.FinishedAt))
            .ToList();

        return new RunDto(
            run.Id, run.AutomationVersionId, run.TriggerType, run.Status.ToString(),
            run.Error, run.CreatedAt, run.StartedAt, run.FinishedAt, logs);
    }

    public async Task RecordStepAsync(AgentStepReportDto report, CancellationToken ct = default)
    {
        var run = await _repo.GetRunAsync(report.RunId, ct);
        if (run is null) return;

        if (run.Status == RunStatus.Dispatched)
        {
            run.Status = RunStatus.Running;
            run.StartedAt ??= DateTimeOffset.UtcNow;
        }

        var status = ParseRunStatus(report.Status);
        var existing = run.StepLogs.FirstOrDefault(l => l.StepId == report.StepId);

        if (existing is null)
        {
            await _repo.AddOrUpdateStepLogAsync(new RunStepLog
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                StepId = report.StepId,
                StepOrder = report.StepOrder,
                Status = status,
                Message = report.Message,
                StartedAt = DateTimeOffset.UtcNow,
                FinishedAt = status is RunStatus.Succeeded or RunStatus.Failed ? DateTimeOffset.UtcNow : null
            }, ct);
        }
        else
        {
            existing.Status = status;
            existing.Message = report.Message;
            if (status is RunStatus.Succeeded or RunStatus.Failed)
                existing.FinishedAt = DateTimeOffset.UtcNow;
        }

        await _repo.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(AgentRunCompletedDto completed, CancellationToken ct = default)
    {
        var run = await _repo.GetRunAsync(completed.RunId, ct);
        if (run is null) return;

        run.Status = ParseRunStatus(completed.Status);
        run.Error = completed.Error;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await _repo.SaveChangesAsync(ct);
    }

    private static RunStatus ParseRunStatus(string value) =>
        Enum.TryParse<RunStatus>(value, ignoreCase: true, out var s) ? s : RunStatus.Failed;
}
