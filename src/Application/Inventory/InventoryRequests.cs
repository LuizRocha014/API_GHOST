namespace Application.Inventory;

public sealed record StockEntryRequest(
    Guid ProductId,
    Guid BranchId,
    decimal Quantity,
    decimal CostPrice,
    DateTime? ExpirationDate,
    DateTime? EntryDate);

public sealed record CreateSaleRequest(
    Guid BranchId,
    IReadOnlyList<SaleLineRequest> Lines);

public sealed record SaleLineRequest(
    Guid ProductId,
    decimal Quantity,
    decimal? UnitPrice);

public sealed record TransferStockRequest(
    Guid SourceBatchId,
    Guid BranchDestId,
    decimal Quantity);

public sealed record CreateProductionRequest(
    Guid BranchId,
    IReadOnlyList<ProductionLineRequest> Lines);

public sealed record ProductionLineRequest(
    Guid ProductInputId,
    Guid BatchInputId,
    decimal QuantityInput,
    Guid ProductOutputId,
    decimal QuantityOutput,
    DateTime? OutputExpirationDate);
