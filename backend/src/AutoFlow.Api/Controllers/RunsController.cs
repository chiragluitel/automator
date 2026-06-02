using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

[ApiController]
[Route("api")]
public class RunsController : ControllerBase
{
    private readonly IRunService _runs;

    public RunsController(IRunService runs) => _runs = runs;

    /// <summary>Manually trigger a run of an automation's active version.</summary>
    [HttpPost("automations/{id:guid}/runs")]
    public async Task<ActionResult<RunDto>> Trigger(Guid id, CancellationToken ct) =>
        Ok(await _runs.TriggerAsync(id, "manual", ct));

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<RunDto>> Get(Guid runId, CancellationToken ct)
    {
        var dto = await _runs.GetAsync(runId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly IAgentConnectionTracker _tracker;
    private readonly ICurrentUser _user;

    public AgentsController(IAgentConnectionTracker tracker, ICurrentUser user)
    {
        _tracker = tracker;
        _user = user;
    }

    /// <summary>Whether the current user has an agent connected (gates the Run button in the UI).</summary>
    [HttpGet("status")]
    public ActionResult<object> Status() =>
        Ok(new { online = _tracker.GetConnectionForUser(_user.Email) is not null });
}
