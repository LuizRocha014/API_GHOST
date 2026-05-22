using Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Consulta e exclusão lógica de produções.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductionsController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public ProductionsController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    /// <summary>Lista produções, opcionalmente por filial.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductionListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductionListDto>>> GetAll(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var items = await _inventory.ListProductionsAsync(branchId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém detalhes de uma produção.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductionDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var production = await _inventory.GetProductionDetailAsync(id, cancellationToken);
        if (production is null)
            return NotFound();

        return Ok(production);
    }

    /// <summary>Remove o registro de produção (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _inventory.SoftDeleteProductionAsync(id, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
