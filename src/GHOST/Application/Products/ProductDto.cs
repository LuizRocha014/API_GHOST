namespace Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    string UnitType,
    bool IsPerishable,
    decimal SalePrice,
    decimal TotalStock,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool Active);
