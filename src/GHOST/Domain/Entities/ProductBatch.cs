namespace Domain.Entities;

/// <summary>
/// Lote de estoque: quantidade e custo são sempre alterados via movimentações.
/// </summary>
public sealed class ProductBatch : Core
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal InitialQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime EntryDate { get; set; }
}
