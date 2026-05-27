namespace Folha.Application.Auth;

public enum EmailVerificationStatus
{
    Success,
    InvalidCode,
    Expired,
    TooManyAttempts,
    NotFound,
}

/// <summary>Resultado da verificação do código de e-mail.</summary>
/// <param name="Status">Desfecho da verificação.</param>
/// <param name="Login">Par de tokens quando <paramref name="Status"/> = Success.</param>
public sealed record VerifyEmailResult(EmailVerificationStatus Status, LoginResponse? Login);
