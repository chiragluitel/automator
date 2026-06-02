using AutoFlow.Application.Abstractions;
using AutoFlow.Domain.Entities;
using AutoFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Infrastructure.Repositories;

public class AutomationRepository : IAutomationRepository
{
    private readonly AutoFlowDbContext _db;

    public AutomationRepository(AutoFlowDbContext db) => _db = db;

    public Task<Automation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Automations.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Automation?> GetWithVersionsAsync(Guid id, CancellationToken ct = default) =>
        _db.Automations
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Automation>> ListAsync(Guid ownerId, CancellationToken ct = default) =>
        await _db.Automations
            .Where(a => a.OwnerId == ownerId)
            .Include(a => a.CurrentVersion)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Automation automation, CancellationToken ct = default) =>
        await _db.Automations.AddAsync(automation, ct);

    public async Task<int> NextVersionNumberAsync(Guid automationId, CancellationToken ct = default)
    {
        var max = await _db.AutomationVersions
            .Where(v => v.AutomationId == automationId)
            .MaxAsync(v => (int?)v.VersionNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task AddVersionAsync(AutomationVersion version, CancellationToken ct = default) =>
        await _db.AutomationVersions.AddAsync(version, ct);

    public Task<AutomationVersion?> GetVersionAsync(Guid versionId, CancellationToken ct = default) =>
        _db.AutomationVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);

    public async Task AddAssetAsync(AutomationAsset asset, CancellationToken ct = default) =>
        await _db.AutomationAssets.AddAsync(asset, ct);

    public async Task AddRunAsync(AutomationRun run, CancellationToken ct = default) =>
        await _db.AutomationRuns.AddAsync(run, ct);

    public Task<AutomationRun?> GetRunAsync(Guid runId, CancellationToken ct = default) =>
        _db.AutomationRuns
            .Include(r => r.StepLogs)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

    public async Task AddOrUpdateStepLogAsync(RunStepLog log, CancellationToken ct = default)
    {
        var exists = await _db.RunStepLogs.AnyAsync(l => l.Id == log.Id, ct);
        if (exists) _db.RunStepLogs.Update(log);
        else await _db.RunStepLogs.AddAsync(log, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
