namespace Folha.Application;

/// <summary>
/// Configuração de e-mail (seção "Email" do appsettings).
/// Se <see cref="SmtpOptions.Host"/> estiver vazio, a API cai no modo de
/// desenvolvimento (loga o e-mail/código em vez de enviar de verdade).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@folha.app";
    public string FromName { get; set; } = "Folha";

    /// <summary>
    /// Quando false, o cadastro já cria o usuário verificado e não envia código
    /// (o login não exige confirmação). Útil enquanto não há SMTP/para testes.
    /// </summary>
    public bool RequireVerification { get; set; } = true;

    /// <summary>Tamanho do código numérico (OTP) de verificação.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Validade do código em minutos.</summary>
    public int CodeTtlMinutes { get; set; } = 15;

    /// <summary>Tentativas erradas permitidas antes de exigir reenvio.</summary>
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
