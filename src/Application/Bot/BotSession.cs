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
public sealed record BotLogEntry(string Phone, string Direction, string Kind, string Body, string? WamId, DateTime CreatedAt);

/// <summary>Persistência do bot: sessões, mensagens já processadas e o histórico da conversa.</summary>
public interface IBotStore
{
    /// <summary>Registra o id da mensagem. Falso = já processada (reenvio do webhook).</summary>
    Task<bool> TryRegisterMessageAsync(string messageId, DateTime receivedAtUtc, CancellationToken ct = default);

    Task<BotSession?> GetSessionAsync(Guid branchId, string phone, CancellationToken ct = default);
    Task SaveSessionAsync(Guid branchId, BotSession session, CancellationToken ct = default);

    Task LogMessageAsync(Guid branchId, string phone, string direction, string kind, string body, string? wamId, CancellationToken ct = default);

    /// <summary>Últimas mensagens da conversa com o número (qualquer das variantes), da mais antiga para a mais nova.</summary>
    Task<IReadOnlyList<BotLogEntry>> ListMessagesAsync(Guid branchId, IReadOnlyList<string> phones, int limit, CancellationToken ct = default);

    /// <summary>Apaga ids processados e mensagens antigas (LGPD: retenção). Devolve quantas linhas saíram.</summary>
    Task<int> PurgeAsync(DateTime processedBeforeUtc, DateTime messagesBeforeUtc, CancellationToken ct = default);
}
