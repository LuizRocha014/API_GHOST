using Domain.Entities;

namespace Domain.Agenda;

/// <summary>
/// Configuração da agenda de uma filial (serviços, boxes, horários, aparência, mensagens do bot…),
/// guardada como documento JSON. É editada inteira pela tela de Ajustes do app.
/// </summary>
public sealed class AgendaSettings : Core
{
    public Guid BranchId { get; set; }
    public string ConfigJson { get; set; } = "{}";
}

/// <summary>Agendamento de um veículo num box.</summary>
public sealed class AgendaBooking : Core
{
    public Guid BranchId { get; set; }

    /// <summary>Dia do atendimento (horário local da estética).</summary>
    public DateTime Date { get; set; }

    /// <summary>Minutos desde 00:00.</summary>
    public int StartMin { get; set; }
    public int EndMin { get; set; }
    public string BoxId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;

    /// <summary>E.164 só com dígitos (5514999999999). Vazio quando não informado.</summary>
    public string Phone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = BookingStatuses.Scheduled;
    public string Origin { get; set; } = BookingOrigins.Counter;

    /// <summary>Agendamento que este substituiu numa remarcação.</summary>
    public Guid? RescheduledFrom { get; set; }

    /// <summary>Cliente cadastrado do agendamento. Nome, telefone e carro continuam copiados aqui como estavam no dia.</summary>
    public Guid? ClientId { get; set; }
    public Guid? VehicleId { get; set; }

    /// <summary>Plano de fidelidade do cliente que cobriu este atendimento (conta no limite do mês). Nulo = cobrado à parte.</summary>
    public Guid? ClientPlanId { get; set; }

    /// <summary>
    /// Adicionais e serviços somados ao principal, como estavam no dia:
    /// <c>[{ "id": "x1", "kind": "extra", "name": "Cera", "price": 30, "duration": 15 }]</c>.
    /// O preço e o fim do agendamento já incluem esses itens.
    /// </summary>
    public string ExtrasJson { get; set; } = "[]";
}

/// <summary>
/// Plano de fidelidade da filial (o modelo oferecido aos clientes): serviços com quantas vezes por mês cada um
/// e o desconto sobre o valor cheio. O preço sai da tabela de serviços por tipo de veículo.
/// </summary>
public sealed class AgendaLoyaltyPlan : Core
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Desconto em % sobre o valor cheio do mês (0 a 100).</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Serviços do plano: <c>[{ "serviceId": "s1", "timesPerMonth": 4 }]</c>.</summary>
    public string ItemsJson { get; set; } = "[]";
}

/// <summary>
/// Plano de fidelidade de um cliente: cópia do modelo (ou montado só para ele) com o preço fechado na adesão
/// para o carro dele. <see cref="EndDate"/> preenchida = plano encerrado.
/// </summary>
public sealed class AgendaClientPlan : Core
{
    public Guid BranchId { get; set; }
    public Guid ClientId { get; set; }

    /// <summary>Modelo de onde saiu. Nulo = plano personalizado.</summary>
    public Guid? PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public string ItemsJson { get; set; } = "[]";

    /// <summary>Carro coberto. Nulo = qualquer carro do cliente do <see cref="VehicleType"/>.</summary>
    public Guid? VehicleId { get; set; }
    public string VehicleType { get; set; } = string.Empty;

    /// <summary>Valor cheio do mês (soma de preço × vezes por mês).</summary>
    public decimal FullPrice { get; set; }

    /// <summary>Valor do mês com o desconto.</summary>
    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Mensalidade paga de um plano de cliente. Mês do plano sem registro aqui = mensalidade pendente.
/// <see cref="Amount"/> guarda o valor do plano no dia do pagamento.
/// </summary>
public sealed class AgendaPlanPayment : Core
{
    public Guid BranchId { get; set; }
    public Guid ClientId { get; set; }
    public Guid ClientPlanId { get; set; }

    /// <summary>Primeiro dia do mês pago.</summary>
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Quando foi marcada como paga (horário local).</summary>
    public DateTime PaidAt { get; set; }
}

/// <summary>Bloqueio pontual (folga, manutenção). <see cref="BoxId"/> nulo = todos os boxes.</summary>
public sealed class AgendaBlock : Core
{
    public Guid BranchId { get; set; }
    public string? BoxId { get; set; }
    public DateTime Date { get; set; }
    public int StartMin { get; set; }
    public int EndMin { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Cliente da agenda, com o consentimento do lembrete (LGPD). O bot acha o cliente pelo telefone; no balcão ele
/// pode ser cadastrado sem telefone (<see cref="Phone"/> vazio).
/// </summary>
public sealed class AgendaClient : Core
{
    public Guid BranchId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool ReminderOptIn { get; set; }
    public DateTime? OptInAt { get; set; }
    public bool Vip { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>O bot não agenda para este número: manda falar com a equipe (abuso, inadimplência…).</summary>
    public bool BotBlocked { get; set; }
}

/// <summary>Carro de um cliente. A placa identifica o carro; sem placa, vale o modelo.</summary>
public sealed class AgendaClientVehicle : Core
{
    public Guid BranchId { get; set; }
    public Guid ClientId { get; set; }
    public string Vehicle { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Outbox das mensagens ao cliente: lembrete de confirmação (D-1), confirmação do agendamento feito no balcão e
/// "seu carro está pronto" (<see cref="ReminderTypes"/>).
/// </summary>
public sealed class AgendaReminder : Core
{
    public Guid BranchId { get; set; }
    public Guid BookingId { get; set; }
    public string Type { get; set; } = ReminderTypes.ConfirmationD1;

    /// <summary>Quando deve sair (horário local).</summary>
    public DateTime ScheduledFor { get; set; }
    public string Status { get; set; } = ReminderStatuses.Pending;
    public int Attempts { get; set; }

    /// <summary>Id da mensagem na Meta, para casar os status de entrega.</summary>
    public string? WamId { get; set; }
    public string? Answer { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public string? Error { get; set; }
}

public static class ReminderTypes
{
    /// <summary>Lembrete na véspera com Confirmar/Remarcar/Cancelar.</summary>
    public const string ConfirmationD1 = "confirmation_d1";

    /// <summary>Confirmação logo depois de agendar pelo balcão.</summary>
    public const string Booked = "booked";

    /// <summary>"Seu carro está pronto" ao concluir o atendimento.</summary>
    public const string Ready = "ready";
}

public static class BookingStatuses
{
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Canceled = "canceled";
    public const string NoShow = "no_show";

    public static readonly IReadOnlyList<string> All = [Scheduled, InProgress, Done, Canceled, NoShow];

    /// <summary>Cancelado e não comparecimento liberam o horário.</summary>
    public static bool IsActive(string status) => status is not (Canceled or NoShow);
}

public static class BookingOrigins
{
    public const string WhatsApp = "whatsapp";
    public const string Counter = "counter";
}

public static class ReminderStatuses
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Delivered = "delivered";
    public const string Read = "read";
    public const string Failed = "failed";
    public const string Canceled = "canceled";

    private static int Rank(string s) => s switch
    {
        Pending => 0,
        Sent => 1,
        Delivered => 2,
        Read => 3,
        _ => 9,
    };

    /// <summary>Status de entrega só andam para frente (a Meta pode entregar "delivered" depois de "read").</summary>
    public static string Advance(string current, string next)
    {
        if (current is Failed or Canceled) return current;
        if (next == Failed) return Failed;
        return Rank(next) > Rank(current) ? next : current;
    }
}

public static class ReminderAnswers
{
    public const string Confirmed = "confirmed";
    public const string Reschedule = "reschedule";
    public const string Canceled = "canceled";

    public static bool IsValid(string? a) => a is Confirmed or Reschedule or Canceled;
}
