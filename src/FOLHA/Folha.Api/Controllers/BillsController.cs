using Folha.Api.Helpers;
using Folha.Application.Bills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Contas a pagar e a receber.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BillsController : ControllerBase
{
    private readonly IBillService _service;

    public BillsController(IBillService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BillDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BillDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<BillDto>> Create([FromBody] CreateBillRequest request, CancellationToken cancellationToken)
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
    public async Task<ActionResult<BillDto>> Update(Guid id, [FromBody] UpdateBillRequest request, CancellationToken cancellationToken)
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
