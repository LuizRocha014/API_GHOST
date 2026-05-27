namespace Folha.Application.Auth;

public sealed record VerifyEmailRequest(string Email, string Code);

public sealed record ResendCodeRequest(string Email);
