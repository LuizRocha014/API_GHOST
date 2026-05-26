namespace Folha.Application.CreditCards;

public sealed record CreditCardDto(
    Guid Id,
    Guid UserId,
    Guid? AccountId,
    string Name,
    string Brand,
    string? LastFour,
    decimal CreditLimit,
    byte ClosingDay,
    byte DueDay,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCreditCardRequest(
    string Name,
    string Brand,
    string? LastFour,
    decimal CreditLimit,
    byte ClosingDay,
    byte DueDay,
    // Conta vinculada é opcional — o cartão pode existir sem conta associada.
    Guid? AccountId = null,
    // Id opcional gerado pelo cliente (offline-first).
    Guid? Id = null);

public sealed record UpdateCreditCardRequest(
    string Name,
    string Brand,
    string? LastFour,
    decimal CreditLimit,
    byte ClosingDay,
    byte DueDay,
    bool IsArchived,
    Guid? AccountId = null);
