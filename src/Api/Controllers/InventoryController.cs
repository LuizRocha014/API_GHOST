using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Operações de estoque, vendas, transferências e produção.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public InventoryController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    /// <summary>Registra entrada de mercadoria no estoque.</summary>
    [HttpPost("entries")]
    [ProducesResponseType(typeof(StockEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockEntryResponse>> RegisterEntry(
        [FromBody] StockEntryRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() =>
            _inventory.RegisterEntryAsync(request, GetUserId(), cancellationToken));
    }

    /// <summary>Registra uma venda e baixa o estoque.</summary>
    [HttpPost("sales")]
    [ProducesResponseType(typeof(CreateSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateSaleResponse>> RegisterSale(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() =>
            _inventory.RegisterSaleAsync(request, GetUserId(), cancellationToken));
    }

    /// <summary>Registra transferência entre filiais.</summary>
    [HttpPost("transfers")]
    [ProducesResponseType(typeof(TransferStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransferStockResponse>> RegisterTransfer(
        [FromBody] TransferStockRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() =>
            _inventory.RegisterTransferAsync(request, GetUserId(), cancellationToken));
    }

    /// <summary>Registra ordem de produção e movimenta insumos/saídas.</summary>
    [HttpPost("productions")]
    [ProducesResponseType(typeof(CreateProductionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateProductionResponse>> RegisterProduction(
        [FromBody] CreateProductionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() =>
            _inventory.RegisterProductionAsync(request, GetUserId(), cancellationToken));
    }

    /// <summary>Obtém detalhes de uma venda.</summary>
    [HttpGet("sales/{id:guid}")]
    [ProducesResponseType(typeof(SaleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleDetailDto>> GetSale(Guid id, CancellationToken cancellationToken)
    {
        var sale = await _inventory.GetSaleDetailAsync(id, cancellationToken);
        if (sale is null)
            return NotFound();

        return Ok(sale);
    }

    /// <summary>Lista movimentações de estoque.</summary>
    [HttpGet("movements")]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> ListMovements(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var list = await _inventory.ListMovementsAsync(branchId, cancellationToken);
        return Ok(list);
    }

    /// <summary>Lista lotes de um produto em uma filial.</summary>
    [HttpGet("batches")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductBatchDto>>> ListBatches(
        [FromQuery] Guid productId,
        [FromQuery] Guid branchId,
        CancellationToken cancellationToken)
    {
        var list = await _inventory.ListBatchesAsync(productId, branchId, cancellationToken);
        return Ok(list);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var id))
            throw new InvalidOperationException("Token inválido: sub ausente.");
        return id;
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action().ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
