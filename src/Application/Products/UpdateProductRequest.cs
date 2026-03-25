namespace Application.Products;

public sealed record UpdateProductRequest(
    string Name,
    string Sku,
    string? Barcode,
    string UnitType,
    bool IsPerishable,
    decimal SalePrice,
    bool Active);
