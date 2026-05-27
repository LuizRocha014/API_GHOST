namespace Folha.Application.Abstractions;

/// <summary>Envio de e-mail transacional (verificação de cadastro, etc.).</summary>
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
