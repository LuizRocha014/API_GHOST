namespace Application.Products;

public sealed record CreateProductRequest(string Name, string Sku, decimal Price, int Stock);
