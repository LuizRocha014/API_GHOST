using Application.Inventory;
using Application.ProductBatches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Lotes de produto por filial.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductBatchesController : ControllerBase
{
    private readonly IProductBatchService _batches;

    public ProductBatchesController(IProductBatchService batches)
    {
        _batches = batches;
    }

    /// <summary>Lista lotes com filtros opcionais.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductBatchDto>>> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        var items = await _batches.ListAsync(branchId, productId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém um lote por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductBatchDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _batches.GetByIdAsync(id, cancellationToken);
        if (batch is null)
            return NotFound();

        return Ok(batch);
    }

    /// <summary>Atualiza metadados do lote (validade, custo, etc.).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateProductBatchMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _batches.UpdateMetadataAsync(id, request, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
