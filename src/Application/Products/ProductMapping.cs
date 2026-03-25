using Domain.Entities;

namespace Application.Products;

internal static class ProductMapping
{
    public static ProductDto ToDto(this Product product, decimal totalStock) =>
        new(
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.UnitType,
            product.IsPerishable,
            product.SalePrice,
            totalStock,
            product.CreatedAt,
            product.UpdatedAt,
            product.Active);
}
