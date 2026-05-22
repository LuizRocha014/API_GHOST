namespace Application.Products;

public sealed record CreateProductRequest(
    string Name,
    string Sku,
    string? Barcode,
    string UnitType,
    bool IsPerishable,
    decimal SalePrice);
