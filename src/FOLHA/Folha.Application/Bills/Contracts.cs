namespace Folha.Application.Bills;

public sealed record BillDto(
    Guid Id,
    Guid UserId,
    Guid? AccountId,
    int? CategoryId,
    Guid? RecurrenceId,
    string Description,
    decimal Amount,
    string Kind,
    DateTime DueDate,
    string Status,
    DateTime? PaidAt,
    Guid? PaidTransactionId,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateBillRequest(
    Guid? AccountId,
    int? CategoryId,
    Guid? RecurrenceId,
    string Description,
    decimal Amount,
    string Kind,
    DateTime DueDate,
    string? Notes = null);

public sealed record UpdateBillRequest(
    Guid? AccountId,
    int? CategoryId,
    Guid? RecurrenceId,
    string Description,
    decimal Amount,
    string Kind,
    DateTime DueDate,
    string Status,
    DateTime? PaidAt,
    Guid? PaidTransactionId,
    string? Notes);
