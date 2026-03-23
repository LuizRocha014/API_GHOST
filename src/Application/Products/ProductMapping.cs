using Domain.Entities;

namespace Application.Products;

internal static class ProductMapping
{
    public static ProductDto ToDto(this Product product) =>
        new(
            product.Id,
            product.Name,
            product.Sku,
            product.Price,
            product.Stock,
            product.CreatedAt);
}
