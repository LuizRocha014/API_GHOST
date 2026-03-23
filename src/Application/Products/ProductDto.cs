namespace Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    int Stock,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool Active);
