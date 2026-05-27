namespace Folha.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default);
    Task<LoginResponse?> RefreshAsync(string refreshToken, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Confirma o código de verificação e, em caso de sucesso, já devolve os tokens.</summary>
    Task<VerifyEmailResult> VerifyEmailAsync(string email, string code, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Reenvia o código de verificação para o e-mail informado.</summary>
    Task<bool> ResendCodeAsync(string email, CancellationToken cancellationToken = default);
}
