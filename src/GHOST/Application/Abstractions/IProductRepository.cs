using Domain.Entities;

namespace Application.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdIncludingInactiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId, CancellationToken cancellationToken = default);
    Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
