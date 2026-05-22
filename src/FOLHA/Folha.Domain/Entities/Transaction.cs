namespace Folha.Domain.Entities;

public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CreditCardId { get; set; }
    public Guid? CreditCardStatementId { get; set; }
    public int CategoryId { get; set; }
    public Guid? BillId { get; set; }
    public Guid? RecurrenceId { get; set; }
    public Guid? ParentTransactionId { get; set; }
    public Guid? TransferId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Place { get; set; }
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "expense";
    public DateTime OccurredAt { get; set; }
    public short? InstallmentNumber { get; set; }
    public short? InstallmentTotal { get; set; }
    public bool IsPending { get; set; }
    public bool IsExcludedFromReports { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
