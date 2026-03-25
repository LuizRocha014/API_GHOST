using Application.Inventory;

namespace Application.ProductBatches;

public interface IProductBatchService
{
    Task<IReadOnlyList<ProductBatchDto>> ListAsync(
        Guid? branchId,
        Guid? productId,
        CancellationToken cancellationToken = default);

    Task<ProductBatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UpdateMetadataAsync(
        Guid id,
        UpdateProductBatchMetadataRequest request,
        CancellationToken cancellationToken = default);
}
