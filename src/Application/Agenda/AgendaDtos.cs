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
    string? RescheduledFrom,
    string? ClientId,
    string? VehicleId,
    string? ClientPlanId,
    IReadOnlyList<BookingExtraDto>? Extras = null);

/// <summary>Adicional ou serviço somado ao agendamento, como estava no dia. <c>Kind</c>: extra ou service.</summary>
public sealed record BookingExtraDto(string Id, string Kind, string Name, decimal? Price, int Duration);

/// <summary>
/// Edição de um agendamento marcado ou em atendimento (serviço, adicionais, veículo, preço, observações). Campo nulo
/// = fica como está. Trocar serviço, tipo de veículo ou adicionais recalcula duração e preço e confere se ainda cabe no
/// mesmo horário e box; <c>Price</c> fixa um preço manual.
/// </summary>
public sealed class EditBookingRequest
{
    public string? ServiceId { get; set; }
    public List<string>? ExtraIds { get; set; }
    public string? VehicleType { get; set; }
    public string? Vehicle { get; set; }
    public string? Plate { get; set; }

    /// <summary>Carro do cliente (o modelo, a placa e o tipo saem do cadastro).</summary>
    public string? VehicleId { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}

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

    /// <summary>Cliente cadastrado escolhido no balcão. Nulo = acha (ou cria) o cliente pelo telefone.</summary>
    public string? ClientId { get; set; }

    /// <summary>Carro do cliente. Nulo = acha pela placa (ou pelo modelo) e cadastra se for novo.</summary>
    public string? VehicleId { get; set; }

    /// <summary>Adicionais do catálogo (cera, aromatização) ou outros serviços somados ao principal.</summary>
    public List<string>? ExtraIds { get; set; }

    /// <summary>Usa o plano de fidelidade do cliente quando ele cobre o serviço e ainda há uso no mês. Falso = cobra à parte.</summary>
    public bool UsePlan { get; set; } = true;
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

public sealed record ClientDto(
    string Id,
    string Phone,
    string Name,
    bool ReminderOptIn,
    string? OptInAt,
    bool Vip,
    string Notes,
    IReadOnlyList<ClientVehicleDto> Vehicles,
    ClientStatsDto Stats,
    IReadOnlyList<string> PlanNames,
    int PendingCharges,
    bool BotBlocked = false);

public sealed record ClientVehicleDto(string Id, string Vehicle, string Plate, string VehicleType, string Color);

/// <summary>Histórico do cliente na filial. <c>LastVisit</c>: dia do último concluído; <c>NextVisit</c>: próximo agendado ('AAAA-MM-DDTHH:mm').</summary>
public sealed record ClientStatsDto(int Done, int NoShow, int Canceled, int Upcoming, decimal TotalSpent, string? LastVisit, string? NextVisit)
{
    public static readonly ClientStatsDto Empty = new(0, 0, 0, 0, 0, null, null);
}

/// <summary>Cadastro do cliente feito no app (tela de Clientes). Os carros vêm inteiros: os que faltarem saem.</summary>
public sealed class ClientFormRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public bool ReminderOptIn { get; set; }
    public bool Vip { get; set; }
    public string? Notes { get; set; }

    /// <summary>O bot não agenda para este cliente (manda falar com a equipe).</summary>
    public bool BotBlocked { get; set; }
    public List<ClientVehicleRequest>? Vehicles { get; set; }
}

public sealed class ClientVehicleRequest
{
    /// <summary>Nulo = carro novo.</summary>
    public string? Id { get; set; }
    public string? Vehicle { get; set; }
    public string? Plate { get; set; }
    public string? VehicleType { get; set; }
    public string? Color { get; set; }
}

/// <summary>Linha agregada dos agendamentos de um cliente (lida pelo repositório).</summary>
public sealed class ClientStatsRow
{
    public Guid ClientId { get; set; }
    public int Done { get; set; }
    public int NoShow { get; set; }
    public int Canceled { get; set; }
    public int Upcoming { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastVisit { get; set; }
    public DateTime? NextDate { get; set; }
    public int? NextStart { get; set; }
}

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
