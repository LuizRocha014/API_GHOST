using Domain.Entities;

namespace Application.Abstractions;

public interface IProductImageRepository
{
    Task<IReadOnlyList<ProductImage>> ListByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductImage> AddAsync(ProductImage image, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ProductImage image, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task UnsetOtherMainsAsync(Guid productId, Guid exceptImageId, CancellationToken cancellationToken = default);
}
