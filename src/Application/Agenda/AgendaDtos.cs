namespace Application.Agenda;

// Contrato JSON (camelCase) usado pelo app Agenda da Estética — ver docs/agenda-api.md.
// Datas de agenda: 'AAAA-MM-DD'; horários: minutos desde 00:00; data e hora local: 'AAAA-MM-DDTHH:mm'.

public sealed record BookingDto(
    string Id,
    string Date,
    int Start,
    int End,
    string BoxId,
    string ServiceId,
    string ServiceName,
    decimal? Price,
    string VehicleType,
    string Vehicle,
    string Plate,
    string ClientName,
    string Phone,
    string Notes,
    string Status,
    string Origin,
    string CreatedAt,
    string UpdatedAt,
    string? RescheduledFrom);

public sealed class NewBookingRequest
{
    public string Date { get; set; } = string.Empty;
    public int Start { get; set; }

    /// <summary>Nulo = a API escolhe o box que deixa menos buraco na agenda.</summary>
    public string? BoxId { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string? Vehicle { get; set; }
    public string? Plate { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public string? Origin { get; set; }
    public bool ReminderOptIn { get; set; } = true;
}

public sealed class RescheduleRequest
{
    public string Date { get; set; } = string.Empty;
    public int Start { get; set; }
    public string? BoxId { get; set; }
}

public sealed class StatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public sealed record SlotOptionDto(int Start, int End, IReadOnlyList<string> BoxIds);

public sealed record BlockDto(string Id, string? BoxId, string Date, int Start, int End, string Reason);

public sealed class NewBlockRequest
{
    public string? BoxId { get; set; }
    public string Date { get; set; } = string.Empty;
    public int Start { get; set; }
    public int End { get; set; }
    public string? Reason { get; set; }
}

public sealed record ClientDto(string Phone, string Name, bool ReminderOptIn, string? OptInAt);

public sealed class ClientRequest
{
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public bool ReminderOptIn { get; set; }
    public string? OptInAt { get; set; }
}

public sealed record ReminderDto(
    string Id,
    string BookingId,
    string Type,
    string ScheduledFor,
    string Status,
    int Attempts,
    string? WamId,
    string? Answer,
    string? AnsweredAt,
    string? Error);

public sealed record ReminderDispatchDto(ReminderDto Reminder, BookingDto Booking);

public sealed class DispatchRequest
{
    /// <summary>Envia os lembretes vencidos até este momento (local). Nulo = agora.</summary>
    public string? Ate { get; set; }

    /// <summary>Força o envio destes lembretes, mesmo antes da hora.</summary>
    public List<string>? Ids { get; set; }
}

public sealed class DeliveryRequest
{
    public string WamId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Erro { get; set; }
}

public sealed class AnswerRequest
{
    public string Resposta { get; set; } = string.Empty;
    public string? Em { get; set; }
}

public sealed record PruneResult(int Removidos);

/// <summary>Filial que o usuário pode abrir no app.</summary>
public sealed record AgendaBranchDto(Guid Id, string Name, Guid CompanyId, string CompanyName);
