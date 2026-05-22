using Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Consulta e exclusão lógica de vendas.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SalesController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public SalesController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    /// <summary>Lista vendas, opcionalmente por filial.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SaleListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SaleListDto>>> GetAll(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var items = await _inventory.ListSalesAsync(branchId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém detalhes de uma venda.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var sale = await _inventory.GetSaleDetailAsync(id, cancellationToken);
        if (sale is null)
            return NotFound();

        return Ok(sale);
    }

    /// <summary>Cancela a venda (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _inventory.SoftDeleteSaleAsync(id, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
