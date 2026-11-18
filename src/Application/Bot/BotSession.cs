using System.Text.Json;
using Application.Agenda;

namespace Application.Bot;

public static class BotStates
{
    public const string Idle = "idle";
    public const string ChoosingService = "choosingService";
    public const string ChoosingVehicleType = "choosingVehicleType";
    public const string ChoosingDay = "choosingDay";
    public const string ChoosingSlot = "choosingSlot";
    public const string AskingVehicle = "askingVehicle";
    public const string Confirming = "confirming";
    public const string WithAttendant = "withAttendant";

    /// <summary>Escolhendo um dos carros cadastrados (ou "outro veículo").</summary>
    public const string ChoosingVehicle = "choosingVehicle";

    /// <summary>Lista dos próximos horários do número.</summary>
    public const string MyBookings = "myBookings";

    /// <summary>Um horário escolhido: remarcar, cancelar (e a confirmação do cancelamento) ou voltar.</summary>
    public const string BookingAction = "bookingAction";

    /// <summary>Escolhendo adicionais (cera, aromatização) antes do dia.</summary>
    public const string ChoosingExtras = "choosingExtras";
}

/// <summary>Conversa em andamento com um número: passo atual e o que já foi escolhido.</summary>
public sealed class BotSession
{
    public string Phone { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public string State { get; set; } = BotStates.Idle;
    public string? ServiceId { get; set; }
    public string? VehicleType { get; set; }
    public string? Date { get; set; }
    public int? SlotStart { get; set; }
    public int SlotPage { get; set; }
    public string? Vehicle { get; set; }
    public string? Plate { get; set; }

    /// <summary>Carro cadastrado escolhido (vai na reserva para ligar ao cliente e ao plano).</summary>
    public string? VehicleId { get; set; }

    /// <summary>Adicionais escolhidos (ids do catálogo).</summary>
    public List<string> ExtraIds { get; set; } = [];

    /// <summary>Agendamento sendo remarcado (o antigo só sai quando o novo é reservado).</summary>
    public string? RescheduleOf { get; set; }

    /// <summary>Opções da última pergunta, para aceitar o número ou o nome digitado.</summary>
    public List<BotOption> Options { get; set; } = [];
    public int InvalidCount { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void Reset()
    {
        State = BotStates.Idle;
        ServiceId = null;
        VehicleType = null;
        Date = null;
        SlotStart = null;
        SlotPage = 0;
        Vehicle = null;
        Plate = null;
        VehicleId = null;
        ExtraIds = [];
        RescheduleOf = null;
        Options = [];
        InvalidCount = 0;
    }

    public string ToJson() => JsonSerializer.Serialize(this, AgendaConfigModel.Json);

    /// <summary>Lê a sessão gravada. <see cref="UpdatedAt"/> vem do JSON, no horário local da agenda.</summary>
    public static BotSession FromJson(string json, string phone)
    {
        var s = JsonSerializer.Deserialize<BotSession>(json, AgendaConfigModel.Json) ?? new BotSession();
        s.Phone = phone;
        return s;
    }
}

/// <summary>Mensagem guardada da conversa. <see cref="CreatedAt"/> em UTC.</summary>
public sealed record BotLogEntry(string Phone, string Direction, string Kind, string Body, string? WamId, DateTime CreatedAt, string? Meta, string? Author);

/// <summary>Uma conversa na caixa de entrada: a última mensagem, a última do cliente e o passo do bot.</summary>
public sealed class BotConversationRow
{
    public string Phone { get; set; } = string.Empty;
    public DateTime LastAt { get; set; }
    public string LastDirection { get; set; } = string.Empty;
    public string LastKind { get; set; } = string.Empty;
    public string LastBody { get; set; } = string.Empty;
    public DateTime? LastInAt { get; set; }
    public int Unread { get; set; }
    public string? State { get; set; }
    public string? DataJson { get; set; }
}

public sealed record BotClientName(string Id, string Phone, string Name);

/// <summary>Extras de uma mensagem guardada: opções (JSON) e quem da equipe escreveu.</summary>
public sealed record BotLogExtra(string? Meta = null, string? Author = null);

/// <summary>Persistência do bot: sessões, mensagens já processadas e o histórico da conversa.</summary>
public interface IBotStore
{
    /// <summary>Registra o id da mensagem. Falso = já processada (reenvio do webhook).</summary>
    Task<bool> TryRegisterMessageAsync(string messageId, DateTime receivedAtUtc, CancellationToken ct = default);

    Task<BotSession?> GetSessionAsync(Guid branchId, string phone, CancellationToken ct = default);
    Task SaveSessionAsync(Guid branchId, BotSession session, CancellationToken ct = default);

    Task LogMessageAsync(
        Guid branchId, string phone, string direction, string kind, string body, string? wamId, BotLogExtra? extra = null, CancellationToken ct = default);

    /// <summary>Últimas mensagens da conversa com o número (qualquer das variantes), da mais antiga para a mais nova.</summary>
    Task<IReadOnlyList<BotLogEntry>> ListMessagesAsync(Guid branchId, IReadOnlyList<string> phones, int limit, CancellationToken ct = default);

    /// <summary>Conversas com mensagem desde <paramref name="sinceUtc"/>, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<BotConversationRow>> ListConversationsAsync(Guid branchId, DateTime sinceUtc, int limit, CancellationToken ct = default);

    /// <summary>A equipe abriu a conversa: o que chegou até agora deixa de contar como não lido.</summary>
    Task MarkSeenAsync(Guid branchId, string phone, DateTime seenAtUtc, CancellationToken ct = default);

    /// <summary>Clientes cadastrados com estes telefones (para pôr nome na caixa de entrada).</summary>
    Task<IReadOnlyList<BotClientName>> ClientNamesAsync(Guid branchId, IReadOnlyList<string> phones, CancellationToken ct = default);

    /// <summary>Apaga ids processados e mensagens antigas (LGPD: retenção). Devolve quantas linhas saíram.</summary>
    Task<int> PurgeAsync(DateTime processedBeforeUtc, DateTime messagesBeforeUtc, CancellationToken ct = default);

    /* ---------- Fila do webhook (persistente) ---------- */

    /// <summary>Grava o item para o worker. Falso = o mesmo id da Meta já estava na fila (webhook reenviado).</summary>
    Task<bool> EnqueueAsync(Domain.Agenda.BotQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Pega o próximo item vencido (pendente, ou "processando" com o prazo estourado: o worker caiu no meio) e o marca como
    /// processando até <paramref name="leaseUntilUtc"/>. Nulo se não há nada.
    /// </summary>
    Task<Domain.Agenda.BotQueueItem?> ClaimNextAsync(DateTime nowUtc, DateTime leaseUntilUtc, CancellationToken ct = default);

    Task CompleteAsync(Guid id, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Volta para a fila em <paramref name="nextAttemptUtc"/>, ou desiste (<paramref name="giveUp"/>: "failed").</summary>
    Task RetryLaterAsync(Guid id, string error, DateTime nextAttemptUtc, bool giveUp, CancellationToken ct = default);

    /// <summary>Pendentes, falhas desde <paramref name="failedSinceUtc"/> e o pendente mais antigo (para o /health).</summary>
    Task<BotQueueHealth> QueueHealthAsync(DateTime failedSinceUtc, CancellationToken ct = default);
}

public sealed record BotQueueHealth(int Pending, int Failed, DateTime? OldestPendingAt);
