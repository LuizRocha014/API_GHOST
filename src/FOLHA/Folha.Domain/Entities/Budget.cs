namespace Folha.Domain.Entities;

public sealed class Budget
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int CategoryId { get; set; }
    public DateTime ReferenceMonth { get; set; }
    public decimal AmountLimit { get; set; }
    public bool Rollover { get; set; }
    public byte AlertThreshold { get; set; } = 80;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
