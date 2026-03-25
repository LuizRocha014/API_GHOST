using Domain;

namespace Application.Inventory;

public sealed record StockEntryResponse(Guid BatchId, Guid MovementId);

public sealed record CreateSaleResponse(Guid SaleId, Guid StockMovementId);

public sealed record TransferStockResponse(Guid DestinationBatchId, Guid StockMovementId);

public sealed record CreateProductionResponse(Guid ProductionId, Guid StockMovementId);

public sealed record SaleDetailDto(
    Guid Id,
    Guid BranchId,
    decimal Total,
    decimal ProfitTotal,
    IReadOnlyList<SaleLineDetailDto> Lines,
    DateTime CreatedAt);

public sealed record SaleLineDetailDto(
    Guid ProductId,
    Guid BatchId,
    decimal Quantity,
    decimal UnitSalePrice,
    decimal BatchCostPrice,
    decimal LineProfit);

public sealed record StockMovementDto(
    Guid Id,
    StockMovementType Type,
    Guid BranchId,
    Guid? BranchDestId,
    DateTime CreatedAt,
    Guid CreatedBy);

public sealed record ProductBatchDto(
    Guid Id,
    Guid ProductId,
    Guid BranchId,
    decimal Quantity,
    decimal InitialQuantity,
    decimal CostPrice,
    DateTime? ExpirationDate,
    DateTime EntryDate,
    bool Active);

public sealed record StockMovementItemDto(
    Guid Id,
    Guid ProductId,
    Guid BatchId,
    decimal Quantity,
    decimal CostPrice);

public sealed record StockMovementDetailDto(
    Guid Id,
    StockMovementType Type,
    Guid BranchId,
    Guid? BranchDestId,
    DateTime CreatedAt,
    Guid CreatedBy,
    IReadOnlyList<StockMovementItemDto> Items);

public sealed record SaleListDto(Guid Id, Guid BranchId, decimal Total, DateTime CreatedAt);

public sealed record ProductionListDto(Guid Id, Guid BranchId, DateTime CreatedAt);

public sealed record ProductionItemDetailDto(
    Guid Id,
    Guid ProductInputId,
    Guid BatchInputId,
    decimal QuantityInput,
    Guid ProductOutputId,
    decimal QuantityOutput,
    Guid OutputBatchId);

public sealed record ProductionDetailDto(
    Guid Id,
    Guid BranchId,
    DateTime CreatedAt,
    IReadOnlyList<ProductionItemDetailDto> Items);
