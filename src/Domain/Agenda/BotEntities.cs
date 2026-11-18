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

    /// <summary>Quando a equipe abriu a conversa pela última vez (UTC): o que chegou depois conta como não lido.</summary>
    public DateTime? SeenAt { get; set; }
}

/// <summary>Id de mensagem do WhatsApp já processada: a Meta reenvia o webhook e o bot não pode agendar duas vezes.</summary>
public sealed class BotProcessedMessage
{
    public string MessageId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}

/// <summary>
/// Item da fila do webhook, gravado antes de responder 200 à Meta: sobrevive a queda e reinício da API. O worker pega um
/// por vez (<see cref="Status"/> "processing" com prazo em <see cref="NextAttemptAt"/>), e o que falha volta com espera
/// crescente até virar "failed".
/// </summary>
public sealed class BotQueueItem
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }

    /// <summary>"message" (mensagem do cliente) ou "status" (entrega/leitura/falha de uma mensagem enviada).</summary>
    public string Kind { get; set; } = BotQueueKinds.Message;

    /// <summary>Id da Meta (message.id, ou wamid:status): o mesmo webhook reenviado não entra duas vezes.</summary>
    public string? ExternalId { get; set; }
    public string PayloadJson { get; set; } = "{}";

    /// <summary>pending, processing, done ou failed.</summary>
    public string Status { get; set; } = BotQueueStatuses.Pending;
    public int Attempts { get; set; }
    public DateTime ReceivedAt { get; set; }

    /// <summary>Pendente: quando pode ser tentado. Processando: até quando o worker tem o item (depois outro pega).</summary>
    public DateTime NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}

public static class BotQueueKinds
{
    public const string Message = "message";
    public const string Status = "status";
}

public static class BotQueueStatuses
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Done = "done";
    public const string Failed = "failed";
}

/// <summary>Mensagem trocada com o cliente (entrada e saída), para a equipe acompanhar a conversa.</summary>
public sealed class BotMessage : Core
{
    public Guid BranchId { get; set; }
    public string Phone { get; set; } = string.Empty;

    /// <summary>"in" (cliente → bot), "out" (bot → cliente) ou "agent" (equipe → cliente, pela caixa de entrada).</summary>
    public string Direction { get; set; } = "in";

    /// <summary>text, list, buttons, template, interactive, ou o tipo recebido (audio, image…).</summary>
    public string Kind { get; set; } = "text";
    public string Body { get; set; } = string.Empty;

    /// <summary>Id da mensagem no WhatsApp (entrada: message.id; saída: wamid devolvido no envio).</summary>
    public string? WamId { get; set; }

    /// <summary>Saída com opções (lista, botões, template): botão e opções em JSON, para a caixa de entrada mostrar.</summary>
    public string? Meta { get; set; }

    /// <summary>Quem da equipe respondeu (Direction = "agent").</summary>
    public string? Author { get; set; }
}
