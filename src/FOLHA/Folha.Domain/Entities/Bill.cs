namespace Folha.Domain.Entities;

public sealed class Bill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public Guid? RecurrenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "payable";
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? PaidAt { get; set; }
    public Guid? PaidTransactionId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
