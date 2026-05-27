namespace Folha.Application.Auth;

/// <summary>
/// Lançada no login quando as credenciais estão corretas mas o e-mail ainda
/// não foi verificado. Ao lançar, um novo código é reenviado automaticamente.
/// </summary>
public sealed class EmailNotVerifiedException : Exception
{
    public string Email { get; }

    public EmailNotVerifiedException(string email)
        : base("Confirme seu e-mail antes de entrar. Enviamos um novo código.")
        => Email = email;
}
