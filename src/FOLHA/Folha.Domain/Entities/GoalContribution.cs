namespace Folha.Domain.Entities;

public sealed class GoalContribution
{
    public Guid Id { get; set; }
    public Guid GoalId { get; set; }
    public Guid? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ContributedAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
