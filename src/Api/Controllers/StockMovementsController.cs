using Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Consulta de movimentações de estoque.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockMovementsController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public StockMovementsController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    /// <summary>Lista movimentações, opcionalmente por filial.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> GetAll(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var items = await _inventory.ListMovementsAsync(branchId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém uma movimentação com itens.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StockMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var movement = await _inventory.GetMovementDetailAsync(id, cancellationToken);
        if (movement is null)
            return NotFound();

        return Ok(movement);
    }
}
