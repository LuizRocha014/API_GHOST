namespace Domain.Entities;

public sealed class StockMovementItem
{
    public Guid Id { get; set; }
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
}
