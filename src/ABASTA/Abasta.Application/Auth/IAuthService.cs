namespace Abasta.Application.Auth;

public sealed record RequestMeta(string? DeviceId, string? UserAgent, string? IpAddress);

public interface IAuthService
{
    /// <summary>Cria empresa + gestor. Retorna tokens, ou null se exigir verificação de e-mail.</summary>
    Task<LoginResponse?> SignupAsync(SignupRequest request, RequestMeta meta, CancellationToken cancellationToken = default);

    Task<LoginResponse?> LoginAsync(LoginRequest request, RequestMeta meta, CancellationToken cancellationToken = default);
    Task<LoginResponse?> RefreshAsync(string refreshToken, RequestMeta meta, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<VerifyEmailResult> VerifyEmailAsync(string email, string code, RequestMeta meta, CancellationToken cancellationToken = default);
    Task<bool> ResendCodeAsync(string email, CancellationToken cancellationToken = default);
}
