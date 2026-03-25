namespace Application.Inventory;

public interface IInventoryService
{
    Task<StockEntryResponse> RegisterEntryAsync(StockEntryRequest request, Guid createdBy, CancellationToken cancellationToken = default);

    Task<CreateSaleResponse> RegisterSaleAsync(CreateSaleRequest request, Guid createdBy, CancellationToken cancellationToken = default);

    Task<TransferStockResponse> RegisterTransferAsync(TransferStockRequest request, Guid createdBy, CancellationToken cancellationToken = default);

    Task<CreateProductionResponse> RegisterProductionAsync(CreateProductionRequest request, Guid createdBy, CancellationToken cancellationToken = default);

    Task<SaleDetailDto?> GetSaleDetailAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(Guid? branchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductBatchDto>> ListBatchesAsync(Guid productId, Guid branchId, CancellationToken cancellationToken = default);

    Task<StockMovementDetailDto?> GetMovementDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleListDto>> ListSalesAsync(Guid? branchId, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteSaleAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionListDto>> ListProductionsAsync(Guid? branchId, CancellationToken cancellationToken = default);

    Task<ProductionDetailDto?> GetProductionDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteProductionAsync(Guid productionId, CancellationToken cancellationToken = default);
}
