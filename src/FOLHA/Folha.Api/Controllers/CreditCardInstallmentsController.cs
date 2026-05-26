using Folha.Api.Helpers;
using Folha.Application.CreditCardInstallments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Planos de parcelamento fixo amarrados a um cartão de crédito.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CreditCardInstallmentsController : ControllerBase
{
    private readonly ICreditCardInstallmentService _service;
    private readonly ILogger<CreditCardInstallmentsController> _logger;

    public CreditCardInstallmentsController(ICreditCardInstallmentService service, ILogger<CreditCardInstallmentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista todos os parcelamentos do usuário.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreditCardInstallmentDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), cancellationToken));

    /// <summary>Lista os parcelamentos de um cartão específico.</summary>
    [HttpGet("by-card/{creditCardId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CreditCardInstallmentDto>>> GetByCard(Guid creditCardId, CancellationToken cancellationToken) =>
        Ok(await _service.ListByCardAsync(creditCardId, this.GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditCardInstallmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CreditCardInstallmentDto>> Create([FromBody] CreateCreditCardInstallmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(this.GetUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar CreditCardInstallment para usuário {UserId}", this.GetUserId());
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditCardInstallmentDto>> Update(Guid id, [FromBody] UpdateCreditCardInstallmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetUserId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao atualizar CreditCardInstallment {InstallmentId} para usuário {UserId}", id, this.GetUserId());
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();
}
