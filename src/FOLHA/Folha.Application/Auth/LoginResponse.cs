namespace Folha.Application.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresInMinutes { get; init; }
    public DateTime RefreshTokenExpiresAt { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
