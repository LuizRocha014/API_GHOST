using Abasta.Application.FuelTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Catálogo de tipos de combustível (lookup global).</summary>
[ApiController]
[Route("api/fuel-types")]
[Authorize]
public sealed class FuelTypesController : ControllerBase
{
    private readonly IFuelTypeService _service;

    public FuelTypesController(IFuelTypeService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FuelTypeDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(cancellationToken));
}
