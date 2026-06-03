using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

[ApiController]
[Route("api/automations")]
public class AutomationsController : ControllerBase
{
    private readonly IAutomationService _service;
    private readonly ITriggerService _triggers;

    public AutomationsController(IAutomationService service, ITriggerService triggers)
    {
        _service = service;
        _triggers = triggers;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AutomationSummaryDto>>> List(CancellationToken ct) =>
        Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AutomationDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Compile a brand-new automation from authored steps + screenshots.</summary>
    [HttpPost("compile")]
    public async Task<ActionResult<AutomationVersionDto>> CompileNew(
        [FromBody] CompileRequestDto request, CancellationToken ct) =>
        Ok(await _service.CompileAndSaveAsync(null, request, ct));

    /// <summary>Compile a new version of an existing automation (e.g. after answering clarifications).</summary>
    [HttpPost("{id:guid}/compile")]
    public async Task<ActionResult<AutomationVersionDto>> CompileRevision(
        Guid id, [FromBody] CompileRequestDto request, CancellationToken ct) =>
        Ok(await _service.CompileAndSaveAsync(id, request, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/activate")]
    public async Task<ActionResult<AutomationVersionDto>> Activate(
        Guid id, Guid versionId, CancellationToken ct) =>
        Ok(await _service.ActivateAsync(id, versionId, ct));

    // ── Trigger endpoints ──────────────────────────────────────────────────

    /// <summary>Returns the trigger watcher config for an automation, or 404 if it is manual.</summary>
    [HttpGet("{id:guid}/trigger")]
    public async Task<ActionResult<AutomationTriggerDto>> GetTrigger(Guid id, CancellationToken ct)
    {
        var dto = await _triggers.GetForAutomationAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Enables or disables the trigger watcher for an automation.</summary>
    [HttpPost("{id:guid}/trigger/{triggerId:guid}/active")]
    public async Task<ActionResult> SetTriggerActive(
        Guid id, Guid triggerId, [FromBody] SetTriggerActiveRequest body, CancellationToken ct)
    {
        await _triggers.SetActiveAsync(triggerId, body.IsActive, ct);
        return NoContent();
    }
}

public record SetTriggerActiveRequest(bool IsActive);
