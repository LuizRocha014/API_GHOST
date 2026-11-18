namespace Application.Auth;

public interface IAuthService
{
    /// <summary>Entra. Senha errada conta tentativa; com 5 seguidas o usuário fica bloqueado por 15 minutos.</summary>
    Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Troca um token ainda válido por um novo (sessão deslizante). Nulo se o usuário não existe mais ou foi desativado.</summary>
    Task<LoginResponse?> RenewAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca a própria senha (confere a atual) e devolve um token novo. Lança <see cref="AuthValidationException"/>
    /// com a mensagem para o usuário.
    /// </summary>
    Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Resultado do login: a sessão, ou a mensagem do erro (<see cref="Locked"/> = bloqueado por tentativas).</summary>
public sealed record LoginOutcome(LoginResponse? Response, string? Error = null, bool Locked = false);

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AuthValidationException(string message) : Exception(message);
