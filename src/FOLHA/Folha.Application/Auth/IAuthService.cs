namespace Folha.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default);
    Task<LoginResponse?> RefreshAsync(string refreshToken, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
