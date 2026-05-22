using Folha.Application.GoalContributions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Aportes feitos em metas.</summary>
[ApiController]
[Route("api/goal-contributions")]
[Authorize]
public sealed class GoalContributionsController : ControllerBase
{
    private readonly IGoalContributionService _service;

    public GoalContributionsController(IGoalContributionService service) => _service = service;

    /// <summary>Lista aportes de uma meta.</summary>
    [HttpGet("by-goal/{goalId:guid}")]
    public async Task<ActionResult<IReadOnlyList<GoalContributionDto>>> GetByGoal(Guid goalId, CancellationToken cancellationToken) =>
        Ok(await _service.ListByGoalAsync(goalId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GoalContributionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<GoalContributionDto>> Create([FromBody] CreateGoalContributionRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GoalContributionDto>> Update(Guid id, [FromBody] UpdateGoalContributionRequest request, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
