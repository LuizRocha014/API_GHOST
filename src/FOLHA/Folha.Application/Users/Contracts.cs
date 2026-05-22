namespace Folha.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string DisplayName,
    string? AvatarUrl,
    string Timezone,
    string Locale,
    string CurrencyCode,
    bool IsActive,
    bool EmailVerified,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FullName,
    string DisplayName,
    string? AvatarUrl = null,
    string? Timezone = null,
    string? Locale = null,
    string? CurrencyCode = null);

public sealed record UpdateUserRequest(
    string FullName,
    string DisplayName,
    string? AvatarUrl,
    string Timezone,
    string Locale,
    string CurrencyCode,
    bool IsActive,
    bool EmailVerified);
