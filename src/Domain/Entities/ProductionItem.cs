namespace Domain.Entities;

public sealed class ProductionItem
{
    public Guid Id { get; set; }
    public Guid ProductionId { get; set; }
    public Guid ProductInputId { get; set; }
    public Guid BatchInputId { get; set; }
    public decimal QuantityInput { get; set; }
    public Guid ProductOutputId { get; set; }
    public decimal QuantityOutput { get; set; }
    public Guid OutputBatchId { get; set; }
}
