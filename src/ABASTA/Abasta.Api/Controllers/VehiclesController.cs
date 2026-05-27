using Abasta.Api.Helpers;
using Abasta.Application.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Veículos da frota. Escopo: empresa do token (claim company_id).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleService _service;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(IVehicleService service, ILogger<VehiclesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(this.GetCompanyId(), skip, take, cancellationToken));

    /// <summary>Veículo que o usuário logado dirige atualmente.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<VehicleDto>> GetMine(CancellationToken cancellationToken)
    {
        var item = await _service.GetMyVehicleAsync(this.GetCompanyId(), this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetCompanyId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Create([FromBody] CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(this.GetCompanyId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar veículo");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> Update(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetCompanyId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao atualizar veículo {VehicleId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Vincula um veículo a um motorista (somente gestor).</summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignDriverRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin()) return Forbid();
        var ok = await _service.AssignDriverAsync(this.GetCompanyId(), id, request.UserId, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Desativa um veículo (somente gestor).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin()) return Forbid();
        return await _service.DeactivateAsync(id, this.GetCompanyId(), cancellationToken) ? NoContent() : NotFound();
    }
}

public sealed record AssignDriverRequest(Guid UserId);
