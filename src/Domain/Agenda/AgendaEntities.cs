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

/// <summary>Cliente da agenda (identificado pelo telefone), com o consentimento do lembrete (LGPD).</summary>
public sealed class AgendaClient : Core
{
    public Guid BranchId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool ReminderOptIn { get; set; }
    public DateTime? OptInAt { get; set; }
}

/// <summary>Outbox do lembrete de confirmação (D-1).</summary>
public sealed class AgendaReminder : Core
{
    public Guid BranchId { get; set; }
    public Guid BookingId { get; set; }
    public string Type { get; set; } = "confirmation_d1";

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
