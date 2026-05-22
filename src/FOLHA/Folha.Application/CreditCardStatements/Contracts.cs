namespace Folha.Application.CreditCardStatements;

public sealed record CreditCardStatementDto(
    Guid Id,
    Guid CreditCardId,
    DateTime ReferenceMonth,
    DateTime ClosingDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    string Status,
    DateTime? PaidAt,
    Guid? PaidFromAccountId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCreditCardStatementRequest(
    Guid CreditCardId,
    DateTime ReferenceMonth,
    DateTime ClosingDate,
    DateTime DueDate,
    decimal TotalAmount = 0,
    decimal PaidAmount = 0,
    string? Status = null);

public sealed record UpdateCreditCardStatementRequest(
    DateTime ClosingDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    string Status,
    DateTime? PaidAt,
    Guid? PaidFromAccountId);
