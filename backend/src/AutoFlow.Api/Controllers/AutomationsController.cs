using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

[ApiController]
[Route("api/automations")]
public class AutomationsController : ControllerBase
{
    private readonly IAutomationService _service;

    public AutomationsController(IAutomationService service) => _service = service;

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
}
