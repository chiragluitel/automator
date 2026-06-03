using System.Text.Json;
using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using AutoFlow.Application.Ir;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;

namespace AutoFlow.Application.Services;

public class AutomationService : IAutomationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAutomationRepository _repo;
    private readonly ICompilationService _compiler;
    private readonly IIrValidator _validator;
    private readonly IAssetStorage _assets;
    private readonly ICurrentUser _user;

    public AutomationService(
        IAutomationRepository repo,
        ICompilationService compiler,
        IIrValidator validator,
        IAssetStorage assets,
        ICurrentUser user)
    {
        _repo = repo;
        _compiler = compiler;
        _validator = validator;
        _assets = assets;
        _user = user;
    }

    public async Task<IReadOnlyList<AutomationSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(_user.UserId, ct);
        return items.Select(a => new AutomationSummaryDto(
            a.Id,
            a.Name,
            a.Description,
            (a.CurrentVersion?.Status ?? VersionStatus.Draft).ToString(),
            a.CurrentVersion?.VersionNumber,
            a.UpdatedAt)).ToList();
    }

    public async Task<AutomationDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _repo.GetWithVersionsAsync(id, ct);
        if (a is null) return null;

        var versions = a.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(ToVersionDto)
            .ToList();

        return new AutomationDetailDto(a.Id, a.Name, a.Description, a.CurrentVersionId, versions);
    }

    public async Task<AutomationVersionDto> CompileAndSaveAsync(
        Guid? automationId, CompileRequestDto request, CancellationToken ct = default)
    {
        // 1. Compile authored steps + screenshots into an IR via Claude.
        var ir = await _compiler.CompileAsync(request, ct);
        var irJson = JsonSerializer.Serialize(ir, Json);

        // 2. Validate against the JSON Schema contract before persisting.
        var errors = _validator.Validate(irJson);
        if (errors.Count > 0)
            throw new InvalidOperationException("Compiled IR failed schema validation: " + string.Join("; ", errors));

        // 3. Resolve or create the parent automation.
        Automation automation;
        if (automationId is { } id)
        {
            automation = await _repo.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Automation {id} not found");
        }
        else
        {
            automation = new Automation
            {
                Id = Guid.NewGuid(),
                Name = ir.Name,
                Description = ir.Description,
                OwnerId = _user.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await _repo.AddAsync(automation, ct);
            // Persist automation first so the version FK (AutomationId) can resolve.
            await _repo.SaveChangesAsync(ct);
        }

        // 4. Status reflects whether Claude still needs answers.
        var status = ir.Steps.Any(s => s.NeedsClarification)
            ? VersionStatus.NeedsClarification
            : VersionStatus.Draft;

        var version = new AutomationVersion
        {
            Id = Guid.NewGuid(),
            AutomationId = automation.Id,
            VersionNumber = await _repo.NextVersionNumberAsync(automation.Id, ct),
            Definition = irJson,
            Status = status,
            CreatedBy = _user.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repo.AddVersionAsync(version, ct);
        await _repo.SaveChangesAsync(ct);

        // Now it's safe to point CurrentVersionId back at the persisted version.
        automation.CurrentVersionId = version.Id;
        await _repo.SaveChangesAsync(ct);

        // 5. Persist authoring screenshots to object storage, linked by step order.
        await StoreScreenshotsAsync(request, ir, version.Id, ct);

        return ToVersionDto(version);
    }

    private async Task StoreScreenshotsAsync(
        CompileRequestDto request, AutomationIr ir, Guid versionId, CancellationToken ct)
    {
        var any = false;
        for (var i = 0; i < request.Steps.Count; i++)
        {
            var raw = request.Steps[i].ScreenshotBase64;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var contentType = request.Steps[i].ScreenshotMediaType ?? "image/png";
            var bytes = Convert.FromBase64String(StripDataUrlPrefix(raw));
            var key = await _assets.PutAsync(bytes, contentType, ct);

            await _repo.AddAssetAsync(new AutomationAsset
            {
                Id = Guid.NewGuid(),
                AutomationVersionId = versionId,
                StepId = i < ir.Steps.Count ? ir.Steps[i].Id : null,
                ObjectKey = key,
                ContentType = contentType,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
            any = true;
        }

        if (any) await _repo.SaveChangesAsync(ct);
    }

    private static string StripDataUrlPrefix(string b64)
    {
        var comma = b64.IndexOf(',');
        return b64.StartsWith("data:") && comma >= 0 ? b64[(comma + 1)..] : b64;
    }

    public async Task<AutomationVersionDto> ActivateAsync(Guid automationId, Guid versionId, CancellationToken ct = default)
    {
        var version = await _repo.GetVersionAsync(versionId, ct)
            ?? throw new KeyNotFoundException($"Version {versionId} not found");

        if (version.AutomationId != automationId)
            throw new InvalidOperationException("Version does not belong to the specified automation.");

        var ir = JsonSerializer.Deserialize<AutomationIr>(version.Definition, Json)!;
        if (ir.Steps.Any(s => s.NeedsClarification))
            throw new InvalidOperationException("Cannot activate a version with unresolved clarifications.");

        version.Status = VersionStatus.Active;
        await _repo.SaveChangesAsync(ct);
        return ToVersionDto(version);
    }

    private static AutomationVersionDto ToVersionDto(AutomationVersion v) => new(
        v.Id,
        v.AutomationId,
        v.VersionNumber,
        v.Status.ToString(),
        JsonSerializer.Deserialize<AutomationIr>(v.Definition, Json)!,
        v.CreatedAt);
}
