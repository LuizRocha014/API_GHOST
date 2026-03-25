using Domain.Entities;

namespace Application.ProductImages;

internal static class ProductImageMapping
{
    public static ProductImageDto ToDto(this ProductImage image) =>
        new(image.Id, image.ProductId, image.Url, image.IsMain, image.Active, image.CreatedAt, image.UpdatedAt);
}
