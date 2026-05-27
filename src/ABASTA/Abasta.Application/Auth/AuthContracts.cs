namespace Abasta.Application.Auth;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Cadastro de uma nova empresa + seu primeiro usuário (gestor).</summary>
public sealed class SignupRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed record VerifyEmailRequest(string Email, string Code);
public sealed record ResendCodeRequest(string Email);

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresInMinutes { get; init; }
    public DateTime RefreshTokenExpiresAt { get; init; }
    public Guid UserId { get; init; }
    public Guid CompanyId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = "collaborator";
}

public enum EmailVerificationStatus
{
    Success,
    InvalidCode,
    Expired,
    TooManyAttempts,
    NotFound,
}

public sealed record VerifyEmailResult(EmailVerificationStatus Status, LoginResponse? Login);

/// <summary>
/// Lançada no login quando as credenciais estão corretas mas o e-mail ainda não
/// foi verificado. Ao lançar, um novo código é reenviado automaticamente.
/// </summary>
public sealed class EmailNotVerifiedException : Exception
{
    public string Email { get; }

    public EmailNotVerifiedException(string email)
        : base("Confirme seu e-mail antes de entrar. Enviamos um novo código.")
        => Email = email;
}
