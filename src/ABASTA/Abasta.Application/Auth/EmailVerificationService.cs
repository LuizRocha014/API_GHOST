using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abasta.Application.Auth;

public sealed class EmailVerificationService : IEmailVerificationService
{
    private readonly IEmailVerificationRepository _repository;
    private readonly IUserRepository _users;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        IEmailVerificationRepository repository,
        IUserRepository users,
        IEmailSender emailSender,
        IOptions<EmailOptions> options,
        ILogger<EmailVerificationService> logger)
    {
        _repository = repository;
        _users = users;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendCodeAsync(User user, CancellationToken cancellationToken = default)
    {
        await _repository.InvalidateAllForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var code = GenerateCode(_options.CodeLength);
        var entity = new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = user.Email,
            CodeHash = HashCode(code),
            Purpose = "signup",
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.CodeTtlMinutes),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        try
        {
            var (subject, html, text) = BuildEmail(user, code);
            await _emailSender.SendAsync(user.Email, user.DisplayName, subject, html, text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar código de verificação para {Email}.", user.Email);
        }
    }

    public async Task<bool> ResendAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(email), cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive || user.EmailVerified)
            return false;

        await SendCodeAsync(user, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<(EmailVerificationStatus Status, User? User)> VerifyAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(email), cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            return (EmailVerificationStatus.NotFound, null);

        if (user.EmailVerified)
            return (EmailVerificationStatus.Success, user);

        var active = await _repository.GetActiveByUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (active is null)
            return (EmailVerificationStatus.NotFound, null);
        if (active.ExpiresAt <= DateTime.UtcNow)
            return (EmailVerificationStatus.Expired, null);
        if (active.Attempts >= _options.MaxAttempts)
            return (EmailVerificationStatus.TooManyAttempts, null);

        var providedHash = HashCode((code ?? string.Empty).Trim());
        if (!HashesEqual(providedHash, active.CodeHash))
        {
            await _repository.IncrementAttemptsAsync(active.Id, cancellationToken).ConfigureAwait(false);
            return (EmailVerificationStatus.InvalidCode, null);
        }

        await _repository.MarkConsumedAsync(active.Id, cancellationToken).ConfigureAwait(false);
        await _users.MarkEmailVerifiedAsync(user.Id, cancellationToken).ConfigureAwait(false);
        user.EmailVerified = true;
        return (EmailVerificationStatus.Success, user);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateCode(int length)
    {
        length = Math.Clamp(length, 4, 9);
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(CultureInfo.InvariantCulture).PadLeft(length, '0');
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private static bool HashesEqual(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private (string Subject, string Html, string Text) BuildEmail(User user, string code)
    {
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? "" : user.DisplayName.Split(' ')[0];
        var greeting = string.IsNullOrEmpty(name) ? "Oi!" : $"Oi, {name}!";
        const string subject = "Seu código de verificação Abasta";
        var ttl = _options.CodeTtlMinutes;

        var text =
            $"{greeting}\n\n" +
            $"Seu código de verificação é: {code}\n\n" +
            $"Ele expira em {ttl} minutos. Se você não criou uma conta na Abasta, ignore este e-mail.\n\n" +
            "— Abasta";

        var html =
            $$"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <body style="margin:0;padding:0;background-color:#FAFAF9;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#FAFAF9;padding:32px 0;">
                <tr><td align="center">
                  <table role="presentation" width="440" cellpadding="0" cellspacing="0" style="background-color:#FFFFFF;border-radius:16px;overflow:hidden;border:1px solid #E7E5E4;">
                    <tr><td style="padding:32px 36px 8px 36px;">
                      <p style="margin:0;font-size:14px;letter-spacing:2px;text-transform:uppercase;color:#C2410C;font-weight:700;">Abasta</p>
                      <h1 style="margin:16px 0 8px 0;font-size:26px;color:#0F172A;">Confirme seu e-mail</h1>
                      <p style="margin:0;font-size:15px;line-height:1.6;color:#57534E;">{{greeting}} Use o código abaixo para confirmar sua conta.</p>
                    </td></tr>
                    <tr><td style="padding:24px 36px;">
                      <div style="background-color:#FFF7ED;border:1px solid #FED7AA;border-radius:12px;padding:20px;text-align:center;">
                        <span style="font-size:36px;font-weight:800;letter-spacing:10px;color:#0F172A;">{{code}}</span>
                      </div>
                    </td></tr>
                    <tr><td style="padding:0 36px 32px 36px;">
                      <p style="margin:0;font-size:13px;line-height:1.6;color:#A8A29E;">O código expira em {{ttl}} minutos. Se você não criou uma conta na Abasta, é só ignorar este e-mail.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        return (subject, html, text);
    }
}
