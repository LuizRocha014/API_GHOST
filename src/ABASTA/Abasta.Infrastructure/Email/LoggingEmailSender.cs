using Abasta.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Abasta.Infrastructure.Email;

/// <summary>
/// Fallback de desenvolvimento: sem SMTP configurado, apenas loga o conteúdo
/// (inclusive o código) para testar o fluxo localmente.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[E-MAIL DEV] SMTP não configurado — e-mail NÃO enviado.\n" +
            "  Para: {Name} <{Email}>\n  Assunto: {Subject}\n  Conteúdo:\n{Body}",
            toName, toEmail, subject, textBody);
        return Task.CompletedTask;
    }
}
