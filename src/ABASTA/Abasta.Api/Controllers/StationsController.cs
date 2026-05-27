using Abasta.Api.Helpers;
using Abasta.Application.Stations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Postos da empresa. Escopo: empresa do token.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StationsController : ControllerBase
{
    private readonly IStationService _service;
    private readonly ILogger<StationsController> _logger;

    public StationsController(IStationService service, ILogger<StationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StationDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetCompanyId(), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<StationDto>> Create([FromBody] CreateStationRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        try
        {
            var created = await _service.CreateAsync(this.GetCompanyId(), request, cancellationToken);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar posto");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StationDto>> Update(Guid id, [FromBody] UpdateStationRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin()) return Forbid();
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetCompanyId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin()) return Forbid();
        return await _service.DeactivateAsync(id, this.GetCompanyId(), cancellationToken) ? NoContent() : NotFound();
    }
}
