namespace Folha.Domain.Entities;

public sealed class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public string? Icon { get; set; }
    public string? ColorHex { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
