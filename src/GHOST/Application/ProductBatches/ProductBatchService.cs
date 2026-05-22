using Application.Abstractions;
using Application.Inventory;

namespace Application.ProductBatches;

public sealed class ProductBatchService : IProductBatchService
{
    private readonly IProductBatchRepository _batches;

    public ProductBatchService(IProductBatchRepository batches)
    {
        _batches = batches;
    }

    public async Task<IReadOnlyList<ProductBatchDto>> ListAsync(
        Guid? branchId,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        var list = await _batches.ListAsync(branchId, productId, cancellationToken).ConfigureAwait(false);
        return list
            .Select(b => new ProductBatchDto(
                b.Id,
                b.ProductId,
                b.BranchId,
                b.Quantity,
                b.InitialQuantity,
                b.CostPrice,
                b.ExpirationDate,
                b.EntryDate,
                b.Active))
            .ToList();
    }

    public async Task<ProductBatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var b = await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (b is null)
            return null;

        return new ProductBatchDto(
            b.Id,
            b.ProductId,
            b.BranchId,
            b.Quantity,
            b.InitialQuantity,
            b.CostPrice,
            b.ExpirationDate,
            b.EntryDate,
            b.Active);
    }

    public Task<bool> UpdateMetadataAsync(
        Guid id,
        UpdateProductBatchMetadataRequest request,
        CancellationToken cancellationToken = default) =>
        _batches.UpdateExpirationAndActiveAsync(id, request.ExpirationDate, request.Active, cancellationToken);
}
