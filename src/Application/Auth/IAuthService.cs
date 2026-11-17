namespace Application.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Troca um token ainda válido por um novo (sessão deslizante). Nulo se o usuário não existe mais ou foi desativado.</summary>
    Task<LoginResponse?> RenewAsync(Guid userId, CancellationToken cancellationToken = default);
}
