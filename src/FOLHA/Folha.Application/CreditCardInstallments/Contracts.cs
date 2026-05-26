namespace Folha.Application.CreditCardInstallments;

public sealed record CreditCardInstallmentDto(
    Guid Id,
    Guid UserId,
    Guid CreditCardId,
    string Description,
    short InstallmentTotal,
    short InstallmentsPaid,
    decimal InstallmentAmount,
    DateTime StartDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCreditCardInstallmentRequest(
    Guid CreditCardId,
    string Description,
    short InstallmentTotal,
    short InstallmentsPaid,
    decimal InstallmentAmount,
    DateTime StartDate,
    // Id opcional gerado pelo cliente (offline-first).
    Guid? Id = null);

public sealed record UpdateCreditCardInstallmentRequest(
    string Description,
    short InstallmentTotal,
    short InstallmentsPaid,
    decimal InstallmentAmount,
    DateTime StartDate);
