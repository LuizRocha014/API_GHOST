namespace Folha.Application.Accounts;

public sealed record AccountDto(
    Guid Id,
    Guid UserId,
    string Name,
    string Kind,
    string? Institution,
    string? Icon,
    string? ColorHex,
    decimal InitialBalance,
    string CurrencyCode,
    bool IsArchived,
    bool IncludeInTotal,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateAccountRequest(
    string Name,
    string Kind,
    string? Institution,
    string? Icon,
    string? ColorHex,
    decimal InitialBalance,
    string? CurrencyCode = null,
    bool IncludeInTotal = true,
    int SortOrder = 0);

public sealed record UpdateAccountRequest(
    string Name,
    string Kind,
    string? Institution,
    string? Icon,
    string? ColorHex,
    decimal InitialBalance,
    string CurrencyCode,
    bool IsArchived,
    bool IncludeInTotal,
    int SortOrder);
