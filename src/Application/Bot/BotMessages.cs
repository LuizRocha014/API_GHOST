namespace Application.Bot;

// Mensagens do bot independentes do canal (Ports & Adapters). O adaptador do WhatsApp
// (Infrastructure/WhatsApp) converte para o JSON da Cloud API; o simulador devolve como estão.

/// <summary>Opção de lista ou botão de resposta: o id volta no webhook quando o cliente toca.</summary>
public sealed record BotOption(string Id, string Title, string? Description = null);

public sealed record BotListSection(string? Title, IReadOnlyList<BotOption> Rows);

public sealed record TemplateButton(string Payload, string Title);

public abstract record OutgoingMessage
{
    /// <summary>text, list, buttons ou template.</summary>
    public abstract string Kind { get; }

    /// <summary>Texto principal (o template mostra a prévia já preenchida).</summary>
    public abstract string Body { get; }
}

public sealed record TextMessage(string Text) : OutgoingMessage
{
    public override string Kind => "text";
    public override string Body => Text;
}

/// <summary>List message: até 10 linhas no total, título da linha até 24 caracteres.</summary>
public sealed record ListMessage(string Text, string Button, IReadOnlyList<BotListSection> Sections, string? Header = null, string? Footer = null)
    : OutgoingMessage
{
    public override string Kind => "list";
    public override string Body => Text;
    public IReadOnlyList<BotOption> Rows => Sections.SelectMany(s => s.Rows).ToList();
}

/// <summary>Reply buttons: até 3 botões, título até 20 caracteres.</summary>
public sealed record ButtonsMessage(string Text, IReadOnlyList<BotOption> Buttons, string? Footer = null) : OutgoingMessage
{
    public override string Kind => "buttons";
    public override string Body => Text;
}

/// <summary>Template aprovado na Meta (necessário fora da janela de 24 h, como o lembrete D-1).</summary>
public sealed record TemplateMessage(
    string Name,
    string Language,
    IReadOnlyList<string> BodyParams,
    IReadOnlyList<TemplateButton> Buttons,
    string Preview) : OutgoingMessage
{
    public override string Kind => "template";
    public override string Body => Preview;
}

/// <summary>
/// Mensagem recebida do cliente. <see cref="SelectionId"/> vem quando ele toca numa opção de lista,
/// num botão ou num botão de template; <see cref="Kind"/> diz o tipo original (text, interactive, button, audio…).
/// </summary>
public sealed record IncomingMessage(
    string Id,
    string Phone,
    DateTime Timestamp,
    string Kind = "text",
    string? Text = null,
    string? SelectionId = null,
    string? SelectionTitle = null,
    string? ProfileName = null);

/// <summary>Status de entrega de uma mensagem enviada (webhook "statuses").</summary>
public sealed record DeliveryStatus(string WamId, string Phone, string Status, DateTime Timestamp, string? Error);

/// <summary>Canal de saída. Devolve o id da mensagem no canal (wamid).</summary>
public interface IMessagingChannel
{
    Task<string> SendAsync(Guid branchId, string phone, OutgoingMessage message, CancellationToken ct = default);
}

/// <summary>Limites da Cloud API para mensagens interativas.</summary>
public static class CloudLimits
{
    public const int ListRows = 10;
    public const int ListRowTitle = 24;
    public const int ListRowDescription = 72;
    public const int ListButton = 20;
    public const int ReplyButtons = 3;
    public const int ReplyButtonTitle = 20;
    public const int Body = 1024;
    public const int Text = 4096;
}
