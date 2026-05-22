namespace Folha.Domain.Entities;

public sealed class CreditCardStatement
{
    public Guid Id { get; set; }
    public Guid CreditCardId { get; set; }
    public DateTime ReferenceMonth { get; set; }
    public DateTime ClosingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "open";
    public DateTime? PaidAt { get; set; }
    public Guid? PaidFromAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
