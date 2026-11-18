namespace Application.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresInMinutes { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>"owner" (dono) ou "staff" (atendente).</summary>
    public string Role { get; init; } = Domain.Entities.UserRoles.Owner;

    /// <summary>Senha provisória: o app pede a troca antes de abrir a agenda (a API recusa as rotas até lá).</summary>
    public bool MustChangePassword { get; init; }
}
