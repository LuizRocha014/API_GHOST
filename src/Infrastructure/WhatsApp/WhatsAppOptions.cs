namespace Infrastructure.WhatsApp;

/// <summary>
/// Seção <c>WhatsApp</c> da configuração. Os segredos (AccessToken, AppSecret, VerifyToken) ficam fora do
/// repositório: <c>dotnet user-secrets</c> no desenvolvimento ou variáveis <c>WhatsApp__AccessToken</c> etc.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>
    /// <c>Log</c> (padrão): nada sai para a Meta, as mensagens só vão para o log e para a tabela BotMessages.
    /// <c>Cloud</c>: envia pela WhatsApp Cloud API.
    /// </summary>
    public string Mode { get; set; } = "Log";

    public string GraphVersion { get; set; } = "v24.0";

    /// <summary>Token permanente do usuário do sistema (Business Manager), com whatsapp_business_messaging.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Chave secreta do app na Meta: valida a assinatura X-Hub-Signature-256 do webhook.</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Texto combinado com a Meta na verificação do webhook (hub.verify_token).</summary>
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>Número do WhatsApp de cada filial (o webhook diz o phone_number_id; o envio precisa dele).</summary>
    public List<WhatsAppNumber> Numbers { get; set; } = [];

    public string ReminderTemplate { get; set; } = Application.Bot.BotTemplates.ReminderName;
    public string TemplateLanguage { get; set; } = Application.Bot.BotTemplates.DefaultLanguage;

    /// <summary>Dias que a conversa fica guardada (LGPD: retenção mínima necessária).</summary>
    public int RetentionDays { get; set; } = 90;

    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);

    public string? PhoneNumberIdFor(Guid branchId) =>
        Numbers.FirstOrDefault(n => n.BranchId == branchId && n.PhoneNumberId.Length > 0)?.PhoneNumberId;

    public Guid? BranchFor(string phoneNumberId) =>
        Numbers.FirstOrDefault(n => n.PhoneNumberId == phoneNumberId)?.BranchId;
}

public sealed class WhatsAppNumber
{
    public string PhoneNumberId { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
}

/// <summary>Erro devolvido pela Cloud API (ou configuração que impede o envio).</summary>
public sealed class WhatsAppException(string message) : Exception(message);
