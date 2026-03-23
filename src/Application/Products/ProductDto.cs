namespace Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    int Stock,
    DateTimeOffset CreatedAt);
