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
    decimal PaidAmount,
    DateTime? PaidAt,
    Guid? PaidTransactionId,
    short? InstallmentCurrent,
    short? InstallmentTotal,
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
    string? Notes = null,
    // Id opcional gerado pelo cliente (offline-first). Quando vier, é usado
    // direto — assim o app não precisa esperar o roundtrip para conhecer o id
    // e ações imediatas (markPaid, partial pay) não disparam swap.
    Guid? Id = null,
    short? InstallmentCurrent = null,
    short? InstallmentTotal = null);

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
    string? Notes,
    decimal? PaidAmount = null,
    short? InstallmentCurrent = null,
    short? InstallmentTotal = null);
