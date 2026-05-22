namespace Domain.Entities;

public sealed class Sale : Core
{
    public Guid BranchId { get; set; }
    public decimal Total { get; set; }
}
