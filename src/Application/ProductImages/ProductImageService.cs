using Application.Abstractions;
using Domain.Entities;

namespace Application.ProductImages;

public sealed class ProductImageService : IProductImageService
{
    private readonly IProductImageRepository _images;
    private readonly IProductRepository _products;

    public ProductImageService(IProductImageRepository images, IProductRepository products)
    {
        _images = images;
        _products = products;
    }

    public async Task<IReadOnlyList<ProductImageDto>> ListByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken).ConfigureAwait(false);
        var list = await _images.ListByProductAsync(productId, cancellationToken).ConfigureAwait(false);
        return list.Select(i => i.ToDto()).ToList();
    }

    public async Task<ProductImageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var image = await _images.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return image?.ToDto();
    }

    public async Task<ProductImageDto> CreateAsync(Guid productId, CreateProductImageRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken).ConfigureAwait(false);

        var utc = DateTime.UtcNow;
        var entity = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Url = request.Url.Trim(),
            IsMain = request.IsMain,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        var created = await _images.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        if (created.IsMain)
            await _images.UnsetOtherMainsAsync(productId, created.Id, cancellationToken).ConfigureAwait(false);

        return created.ToDto();
    }

    public async Task<ProductImageDto?> UpdateAsync(Guid id, UpdateProductImageRequest request, CancellationToken cancellationToken = default)
    {
        var image = await _images.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (image is null)
            return null;

        image.Url = request.Url.Trim();
        image.IsMain = request.IsMain;
        image.Active = request.Active;
        image.UpdatedAt = DateTime.UtcNow;

        var ok = await _images.UpdateAsync(image, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return null;

        if (image.IsMain && image.Active)
            await _images.UnsetOtherMainsAsync(image.ProductId, image.Id, cancellationToken).ConfigureAwait(false);

        return image.ToDto();
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _images.SoftDeleteAsync(id, cancellationToken);

    private async Task EnsureProductExistsAsync(Guid productId, CancellationToken cancellationToken)
    {
        var p = await _products.GetByIdAsync(productId, cancellationToken).ConfigureAwait(false);
        if (p is null)
            throw new InvalidOperationException("Produto não encontrado.");
    }
}
