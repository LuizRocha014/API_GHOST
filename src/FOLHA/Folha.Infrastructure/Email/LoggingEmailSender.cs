using Folha.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Folha.Infrastructure.Email;

/// <summary>
/// Fallback de desenvolvimento: quando o SMTP não está configurado, em vez de
/// enviar o e-mail apenas loga o conteúdo (incluindo o código) para que dê
/// para testar o fluxo localmente sem um servidor de e-mail.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[E-MAIL DEV] SMTP não configurado — e-mail NÃO enviado.\n" +
            "  Para: {Name} <{Email}>\n  Assunto: {Subject}\n  Conteúdo:\n{Body}",
            toName, toEmail, subject, textBody);
        return Task.CompletedTask;
    }
}
