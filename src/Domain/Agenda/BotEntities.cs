using Domain.Entities;

namespace Domain.Agenda;

/// <summary>
/// Conversa do bot com um número numa filial: em que passo está e o que já foi escolhido
/// (<see cref="DataJson"/>). Uma linha por filial e telefone.
/// </summary>
public sealed class BotSessionRecord : Core
{
    public Guid BranchId { get; set; }

    /// <summary>wa_id do WhatsApp (E.164 só com dígitos).</summary>
    public string Phone { get; set; } = string.Empty;
    public string State { get; set; } = "idle";
    public string DataJson { get; set; } = "{}";
}

/// <summary>Id de mensagem do WhatsApp já processada: a Meta reenvia o webhook e o bot não pode agendar duas vezes.</summary>
public sealed class BotProcessedMessage
{
    public string MessageId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}

/// <summary>Mensagem trocada com o cliente (entrada e saída), para a equipe acompanhar a conversa.</summary>
public sealed class BotMessage : Core
{
    public Guid BranchId { get; set; }
    public string Phone { get; set; } = string.Empty;

    /// <summary>"in" (cliente → bot) ou "out" (bot → cliente).</summary>
    public string Direction { get; set; } = "in";

    /// <summary>text, list, buttons, template, interactive, ou o tipo recebido (audio, image…).</summary>
    public string Kind { get; set; } = "text";
    public string Body { get; set; } = string.Empty;

    /// <summary>Id da mensagem no WhatsApp (entrada: message.id; saída: wamid devolvido no envio).</summary>
    public string? WamId { get; set; }
}
