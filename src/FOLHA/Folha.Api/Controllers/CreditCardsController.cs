using Folha.Api.Helpers;
using Folha.Application.CreditCards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Cartões de crédito do usuário.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CreditCardsController : ControllerBase
{
    private readonly ICreditCardService _service;

    public CreditCardsController(ICreditCardService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreditCardDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditCardDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CreditCardDto>> Create([FromBody] CreateCreditCardRequest request, CancellationToken cancellationToken)
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
    public async Task<ActionResult<CreditCardDto>> Update(Guid id, [FromBody] UpdateCreditCardRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken) =>
        await _service.ArchiveAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();
}
