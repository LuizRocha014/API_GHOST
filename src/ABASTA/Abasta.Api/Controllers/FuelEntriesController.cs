using Abasta.Api.Helpers;
using Abasta.Application.FuelEntries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Notas de abastecimento — coração do Abasta. Escopo: empresa do token (claim company_id).</summary>
[ApiController]
[Route("api/fuel-entries")]
[Authorize]
public sealed class FuelEntriesController : ControllerBase
{
    private readonly IFuelEntryService _service;
    private readonly ILogger<FuelEntriesController> _logger;

    public FuelEntriesController(IFuelEntryService service, ILogger<FuelEntriesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista paginada. Filtre por colaborador (userId) e/ou veículo (vehicleId).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FuelEntryDto>>> GetAll(
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? vehicleId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(this.GetCompanyId(), userId, vehicleId, skip, take, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FuelEntryDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetCompanyId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<FuelEntryDto>> Create([FromBody] CreateFuelEntryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(this.GetCompanyId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar abastecimento");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FuelEntryDto>> Update(Guid id, [FromBody] UpdateFuelEntryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetCompanyId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao atualizar abastecimento {EntryId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, this.GetCompanyId(), cancellationToken) ? NoContent() : NotFound();
}
