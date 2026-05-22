namespace Folha.Domain.Entities;

public sealed class Transfer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
