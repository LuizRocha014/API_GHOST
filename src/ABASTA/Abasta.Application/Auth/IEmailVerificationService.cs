using Abasta.Domain.Entities;

namespace Abasta.Application.Auth;

public interface IEmailVerificationService
{
    /// <summary>Gera um novo código, invalida os antigos e envia por e-mail.</summary>
    Task SendCodeAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Reenvia o código para o e-mail informado (true se havia o que reenviar).</summary>
    Task<bool> ResendAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Valida o código e, em caso de sucesso, marca o e-mail como verificado.</summary>
    Task<(EmailVerificationStatus Status, User? User)> VerifyAsync(string email, string code, CancellationToken cancellationToken = default);
}
