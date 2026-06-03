using AutoFlow.Application.Abstractions;
using AutoFlow.Domain.Entities;
using AutoFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Infrastructure.Repositories;

public class TriggerRepository : ITriggerRepository
{
    private readonly AutoFlowDbContext _db;

    public TriggerRepository(AutoFlowDbContext db) => _db = db;

    public Task<AutomationTrigger?> GetByAutomationIdAsync(Guid automationId, CancellationToken ct = default) =>
        _db.AutomationTriggers.FirstOrDefaultAsync(t => t.AutomationId == automationId, ct);

    public Task<AutomationTrigger?> GetByIdAsync(Guid triggerId, CancellationToken ct = default) =>
        _db.AutomationTriggers.FirstOrDefaultAsync(t => t.Id == triggerId, ct);

    public async Task<IReadOnlyList<AutomationTrigger>> GetActiveForUserEmailAsync(string userEmail, CancellationToken ct = default) =>
        await _db.AutomationTriggers
            .Where(t => t.IsActive && t.Automation!.Owner!.Email == userEmail)
            .Include(t => t.Automation)
            .ToListAsync(ct);

    public async Task UpsertAsync(AutomationTrigger trigger, CancellationToken ct = default)
    {
        var exists = await _db.AutomationTriggers.AnyAsync(t => t.Id == trigger.Id, ct);
        if (exists) _db.AutomationTriggers.Update(trigger);
        else await _db.AutomationTriggers.AddAsync(trigger, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
