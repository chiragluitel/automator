using System.Text.Json;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using AutoFlow.Application.Ir;
using AutoFlow.Domain.Entities;

namespace AutoFlow.Application.Services;

public class TriggerService : ITriggerService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ITriggerRepository _repo;

    public TriggerService(ITriggerRepository repo) => _repo = repo;

    public async Task UpsertForActivationAsync(Guid automationId, AutomationIr ir, CancellationToken ct = default)
    {
        if (ir.Trigger.Type == "manual") return;

        var conditions = ExtractConditions(ir.Trigger);
        var conditionsJson = JsonSerializer.Serialize(conditions, Json);

        var existing = await _repo.GetByAutomationIdAsync(automationId, ct);
        if (existing is null)
        {
            existing = new AutomationTrigger
            {
                Id = Guid.NewGuid(),
                AutomationId = automationId,
                Type = ir.Trigger.Type,
                IsActive = true,
                Conditions = conditionsJson,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
        else
        {
            existing.Type = ir.Trigger.Type;
            existing.IsActive = true;
            existing.Conditions = conditionsJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _repo.UpsertAsync(existing, ct);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TriggerConfigDto>> GetActiveForUserEmailAsync(string userEmail, CancellationToken ct = default)
    {
        var triggers = await _repo.GetActiveForUserEmailAsync(userEmail, ct);
        return triggers.Select(ToConfigDto).ToList();
    }

    public async Task<AutomationTriggerDto?> GetForAutomationAsync(Guid automationId, CancellationToken ct = default)
    {
        var t = await _repo.GetByAutomationIdAsync(automationId, ct);
        return t is null ? null : ToDto(t);
    }

    public async Task SetActiveAsync(Guid triggerId, bool isActive, CancellationToken ct = default)
    {
        var trigger = await _repo.GetByIdAsync(triggerId, ct);
        if (trigger is null) return;
        trigger.IsActive = isActive;
        trigger.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private TriggerConfigDto ToConfigDto(AutomationTrigger t) => new(
        t.Id,
        t.AutomationId,
        t.Type,
        Deserialize(t.Conditions)
    );

    private AutomationTriggerDto ToDto(AutomationTrigger t) => new(
        t.Id,
        t.AutomationId,
        t.Type,
        t.IsActive,
        Deserialize(t.Conditions)
    );

    private static Dictionary<string, string> Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Json) ?? new(); }
        catch { return new(); }
    }

    /// <summary>
    /// Flattens an IrTrigger's conditions list into a key→value dict that maps
    /// directly to read_email / watcher filter param names.
    ///
    /// Mapping rules:
    ///   from + any op     → "from"             (watcher always does partial match)
    ///   subject + contains → "subject_contains"
    ///   subject + equals   → "subject_contains" (same filter key)
    ///   body + contains    → "body_contains"
    ///   folder + any       → "folder"
    ///   is_unread, is_flagged, has_attachment, importance, category → kept as-is
    /// </summary>
    private static Dictionary<string, string> ExtractConditions(IrTrigger trigger)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Source field carries folder hint (e.g. trigger.source = "Inbox")
        if (!string.IsNullOrWhiteSpace(trigger.Source)
            && !trigger.Source.Equals("outlook", StringComparison.OrdinalIgnoreCase))
        {
            dict["folder"] = trigger.Source;
        }

        foreach (var c in trigger.Conditions)
        {
            var value = c.Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(value)) continue;

            var key = c.Field.ToLowerInvariant() switch
            {
                "from"           => "from",
                "to"             => "to",
                "subject" when c.Op == "contains" => "subject_contains",
                "subject"        => "subject_contains",
                "body" when c.Op == "contains"    => "body_contains",
                "body"           => "body_contains",
                "folder"         => "folder",
                "is_unread"      => "is_unread",
                "is_flagged"     => "is_flagged",
                "has_attachment" => "has_attachment",
                "importance"     => "importance",
                "category"       => "category",
                _                => c.Field.ToLowerInvariant()
            };

            dict[key] = value;
        }

        return dict;
    }
}
