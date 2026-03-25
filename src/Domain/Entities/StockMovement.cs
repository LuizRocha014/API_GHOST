using Domain;

namespace Domain.Entities;

public sealed class StockMovement
{
    public Guid Id { get; set; }
    public StockMovementType Type { get; set; }
    public Guid BranchId { get; set; }
    public Guid? BranchDestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}
