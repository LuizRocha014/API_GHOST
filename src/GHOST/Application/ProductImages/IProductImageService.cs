namespace Application.ProductImages;

public interface IProductImageService
{
    Task<IReadOnlyList<ProductImageDto>> ListByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductImageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductImageDto> CreateAsync(Guid productId, CreateProductImageRequest request, CancellationToken cancellationToken = default);
    Task<ProductImageDto?> UpdateAsync(Guid id, UpdateProductImageRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
