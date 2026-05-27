using System.Net;
using System.Net.Mail;
using Folha.Application;
using Folha.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Folha.Infrastructure.Email;

/// <summary>
/// Envio real via SMTP (System.Net.Mail). Compatível com Gmail, Outlook,
/// Mailtrap, Brevo etc. — basta configurar a seção "Email:Smtp".
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? toEmail : toName));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));

        using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrWhiteSpace(_options.Smtp.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password),
        };

        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("E-mail '{Subject}' enviado para {Email}.", subject, toEmail);
    }
}
