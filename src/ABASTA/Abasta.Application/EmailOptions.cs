namespace Abasta.Application;

/// <summary>
/// Configuração de e-mail (seção "Email"). Se <see cref="SmtpOptions.Host"/>
/// estiver vazio, a API cai no modo dev (loga o código em vez de enviar).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@abasta.com.br";
    public string FromName { get; set; } = "Abasta";

    /// <summary>
    /// Quando false, o cadastro já cria o usuário verificado e o login não exige
    /// confirmação. Útil enquanto não há SMTP/para testes.
    /// </summary>
    public bool RequireVerification { get; set; } = true;

    public int CodeLength { get; set; } = 6;
    public int CodeTtlMinutes { get; set; } = 15;
    public int MaxAttempts { get; set; } = 5;

    public SmtpOptions Smtp { get; set; } = new();

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(Smtp.Host);

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
