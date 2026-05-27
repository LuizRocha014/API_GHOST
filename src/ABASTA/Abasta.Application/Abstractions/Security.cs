using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenService
{
    /// <summary>Emite um JWT com claims sub, email, name, company_id e role.</summary>
    string CreateToken(User user);
}

/// <summary>Envio de e-mail transacional (código de verificação, convites).</summary>
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}
