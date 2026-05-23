namespace Folha.Application.CreditCards;

public sealed record CreditCardDto(
    Guid Id,
    Guid UserId,
    Guid AccountId,
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
    Guid AccountId,
    string Name,
    string Brand,
    string? LastFour,
    decimal CreditLimit,
    byte ClosingDay,
    byte DueDay,
    // Id opcional gerado pelo cliente (offline-first).
    Guid? Id = null);

public sealed record UpdateCreditCardRequest(
    Guid AccountId,
    string Name,
    string Brand,
    string? LastFour,
    decimal CreditLimit,
    byte ClosingDay,
    byte DueDay,
    bool IsArchived);
