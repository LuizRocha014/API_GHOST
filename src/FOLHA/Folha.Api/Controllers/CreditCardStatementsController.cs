using Folha.Application.CreditCardStatements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Faturas mensais de cartão de crédito.</summary>
[ApiController]
[Route("api/credit-card-statements")]
[Authorize]
public sealed class CreditCardStatementsController : ControllerBase
{
    private readonly ICreditCardStatementService _service;

    public CreditCardStatementsController(ICreditCardStatementService service) => _service = service;

    /// <summary>Lista faturas de um cartão.</summary>
    [HttpGet("by-card/{creditCardId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CreditCardStatementDto>>> GetByCard(Guid creditCardId, CancellationToken cancellationToken) =>
        Ok(await _service.ListByCardAsync(creditCardId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditCardStatementDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CreditCardStatementDto>> Create([FromBody] CreateCreditCardStatementRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditCardStatementDto>> Update(Guid id, [FromBody] UpdateCreditCardStatementRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
