namespace Domain.Entities;

public sealed class Product : Core
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    /// <summary>UN, KG ou LT.</summary>
    public string UnitType { get; set; } = "UN";
    public bool IsPerishable { get; set; }
    public decimal SalePrice { get; set; }
}
