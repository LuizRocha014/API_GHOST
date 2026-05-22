using Folha.Api.Helpers;
using Folha.Application.Budgets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Orçamentos mensais por categoria.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BudgetsController : ControllerBase
{
    private readonly IBudgetService _service;

    public BudgetsController(IBudgetService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BudgetDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create([FromBody] CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(this.GetUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BudgetDto>> Update(Guid id, [FromBody] UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetUserId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();
}
