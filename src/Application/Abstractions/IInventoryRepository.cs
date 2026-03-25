using Domain.Entities;

namespace Application.Abstractions;

public interface IInventoryRepository
{
    Task<decimal> GetTotalStockForProductAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductBatch>> GetBatchesForProductAndBranchAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default);

    /// <summary>Todos os lotes do produto na filial (inclui zerados/inativos) para rastreabilidade.</summary>
    Task<IReadOnlyList<ProductBatch>> ListAllBatchesForProductAndBranchAsync(
        Guid productId,
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>Entrada: novo lote + movimentação ENTRY (atômico).</summary>
    Task<RegisterEntryResult> RegisterEntryAsync(RegisterEntryCommand command, CancellationToken cancellationToken = default);

    /// <summary>Venda: FEFO já resolvido no serviço; persiste venda, itens, baixa de lotes e EXIT.</summary>
    Task<RegisterSaleResult> RegisterSaleAsync(RegisterSaleCommand command, CancellationToken cancellationToken = default);

    /// <summary>Transferência: baixa no lote de origem e novo lote no destino + TRANSFER.</summary>
    Task<RegisterTransferResult> RegisterTransferAsync(RegisterTransferCommand command, CancellationToken cancellationToken = default);

    /// <summary>Produção: consome lotes de entrada, cria lote(s) de saída + PRODUCTION.</summary>
    Task<RegisterProductionResult> RegisterProductionAsync(RegisterProductionCommand command, CancellationToken cancellationToken = default);

    Task<Sale?> GetSaleByIdAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleItem>> GetSaleItemsAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> ListMovementsAsync(Guid? branchId, CancellationToken cancellationToken = default);

    Task<StockMovement?> GetMovementByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementItem>> GetMovementItemsAsync(Guid movementId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Sale>> ListSalesAsync(Guid? branchId, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteSaleAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Production>> ListProductionsAsync(Guid? branchId, CancellationToken cancellationToken = default);

    Task<Production?> GetProductionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionItem>> GetProductionItemsAsync(Guid productionId, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteProductionAsync(Guid productionId, CancellationToken cancellationToken = default);
}

public sealed record RegisterEntryCommand(
    Guid ProductId,
    Guid BranchId,
    decimal Quantity,
    decimal CostPrice,
    DateTime? ExpirationDate,
    DateTime EntryDate,
    Guid CreatedBy);

public sealed record RegisterSaleLine(
    Guid ProductId,
    Guid BatchId,
    decimal Quantity,
    decimal UnitSalePrice,
    decimal BatchCostPrice);

public sealed record RegisterSaleCommand(
    Guid BranchId,
    Guid CreatedBy,
    IReadOnlyList<RegisterSaleLine> Lines);

public sealed record RegisterTransferCommand(
    Guid SourceBatchId,
    Guid BranchDestId,
    decimal Quantity,
    Guid CreatedBy);

public sealed record RegisterProductionLine(
    Guid ProductInputId,
    Guid BatchInputId,
    decimal QuantityInput,
    Guid ProductOutputId,
    decimal QuantityOutput,
    decimal OutputCostPricePerUnit,
    DateTime? OutputExpirationDate);

public sealed record RegisterProductionCommand(
    Guid BranchId,
    Guid CreatedBy,
    IReadOnlyList<RegisterProductionLine> Lines);

public sealed record RegisterEntryResult(ProductBatch Batch, Guid StockMovementId);

public sealed record RegisterSaleResult(Sale Sale, Guid StockMovementId);

public sealed record RegisterTransferResult(ProductBatch DestinationBatch, Guid StockMovementId);

public sealed record RegisterProductionResult(Production Production, Guid StockMovementId);
