using Domain.Entities;

namespace Application.Abstractions;

public interface IProductBatchRepository
{
    Task<IReadOnlyList<ProductBatch>> ListAsync(
        Guid? branchId,
        Guid? productId,
        CancellationToken cancellationToken = default);

    Task<ProductBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdateExpirationAndActiveAsync(Guid id, DateTime? expirationDate, bool active, CancellationToken cancellationToken = default);
}
