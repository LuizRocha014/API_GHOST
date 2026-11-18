using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Agenda;

/// <summary>
/// Parte da configuração da agenda que a API precisa para as regras (disponibilidade, lembrete).
/// O documento completo (aparência, mensagens, painel…) é guardado e devolvido como veio do app.
/// </summary>
public sealed class AgendaConfigModel
{
    public string BusinessName { get; set; } = "Minha Estética";

    /// <summary>Intervalo entre horários de início (15, 30 ou 60).</summary>
    public int Step { get; set; } = 30;
    public List<BoxModel> Boxes { get; set; } = [];

    /// <summary>Índice 0 = domingo … 6 = sábado. Nulo = fechado. Cada item é [abre, fecha] em minutos.</summary>
    public List<int[]?> Hours { get; set; } = [];
    public List<ServiceModel> Services { get; set; } = [];
    public ReminderPolicyModel Reminder { get; set; } = new();
    public BotSettingsModel Bot { get; set; } = new();
    public TermsModel? Terms { get; set; }

    /// <summary>Textos do bot e do lembrete (Ajustes → Bot e mensagens). Vazio = padrão.</summary>
    public BotTextsModel Texts { get; set; } = new();

    /// <summary>Endereço, mapa, pagamento e recado que o bot manda quando o cliente pede informações.</summary>
    public BusinessInfoModel Info { get; set; } = new();

    /// <summary>Bloqueios que se repetem toda semana: intervalo de almoço (todos os boxes), manutenção de um box…</summary>
    public List<RecurringBlockModel> RecurringBlocks { get; set; } = [];

    /// <summary>Feriados em que a estética fecha.</summary>
    public HolidayPolicyModel Holidays { get; set; } = new();

    /// <summary>Folga entre dois atendimentos no mesmo box (limpeza), em minutos.</summary>
    public int BufferMin { get; set; }

    /// <summary>Mensagens automáticas além do lembrete: confirmação do balcão e carro pronto.</summary>
    public NotifyModel Notify { get; set; } = new();

    /// <summary>Mensalidades dos planos: tolerância, suspensão por atraso e avisos pelo WhatsApp.</summary>
    public PlanBillingModel PlanBilling { get; set; } = new();

    /// <summary>Adicionais que entram junto com o serviço (cera, aromatização…), com preço e tempo por tipo de veículo.</summary>
    public List<ExtraModel> Extras { get; set; } = [];

    public ExtraModel? ExtraById(string? id) => Extras.FirstOrDefault(x => x.Id == id);

    /// <summary>Por tipo ("SUV"): nome mostrado e se está ativo. Ausente = nome padrão e ativo.</summary>
    public Dictionary<string, VehicleTypeConfigModel> VehicleTypes { get; set; } = [];

    /// <summary>Tipos de veículo, na ordem em que aparecem para o cliente.</summary>
    public static readonly IReadOnlyList<string> AllVehicleTypes = ["Hatch", "Sedan", "SUV", "Picape", "Moto"];

    public bool VehicleEnabled(string type) => !VehicleTypes.TryGetValue(type, out var c) || c.Enabled;

    public string VehicleLabel(string type) =>
        VehicleTypes.TryGetValue(type, out var c) && !string.IsNullOrWhiteSpace(c.Label) ? c.Label! : type;

    /// <summary>Tipos ativos (todos, se a configuração desligou todos).</summary>
    public IReadOnlyList<string> EnabledVehicleTypes
    {
        get
        {
            var list = AllVehicleTypes.Where(VehicleEnabled).ToList();
            return list.Count == 0 ? AllVehicleTypes : list;
        }
    }

    private static readonly Dictionary<string, string> DefaultStatusLabels = new()
    {
        ["scheduled"] = "Agendado",
        ["inProgress"] = "Em atendimento",
        ["done"] = "Concluído",
        ["late"] = "Atrasado",
        ["canceled"] = "Cancelado",
        ["noShow"] = "Não compareceu",
    };

    /// <summary>Nome do status do agendamento ("scheduled", "in_progress"…) como a filial chama.</summary>
    public string StatusText(string bookingStatus)
    {
        var key = bookingStatus switch
        {
            BookingStatusKeys.InProgress => "inProgress",
            BookingStatusKeys.NoShow => "noShow",
            _ => bookingStatus,
        };
        if (Terms?.StatusLabels is { } custom && custom.TryGetValue(key, out var label) && !string.IsNullOrWhiteSpace(label)) return label;
        return DefaultStatusLabels.GetValueOrDefault(key, bookingStatus);
    }

    public ServiceModel? ServiceById(string? id) => Services.FirstOrDefault(s => s.Id == id);
    public BoxModel? BoxById(string? id) => Boxes.FirstOrDefault(b => b.Id == id);

    public string BoxName(string? id) => id is null
        ? $"Todos os {(Terms?.BoxPlural ?? "Boxes").ToLowerInvariant()}"
        : BoxById(id)?.Name ?? $"{Terms?.BoxSingular ?? "Box"} removido";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static AgendaConfigModel Parse(string json) =>
        JsonSerializer.Deserialize<AgendaConfigModel>(json, Json) ?? throw new AgendaValidationException("Configuração inválida.");

    /// <summary>Configuração inicial de uma filial nova (mesma do app).</summary>
    public static string DefaultJson(string businessName)
    {
        var node = JsonNode.Parse(DefaultConfigJson)!.AsObject();
        node["businessName"] = string.IsNullOrWhiteSpace(businessName) ? "Minha Estética" : businessName;
        return node.ToJsonString(Json);
    }

    private const string DefaultConfigJson = """
        {
          "businessName": "Minha Estética",
          "step": 30,
          "boxes": [
            { "id": "b1", "name": "Box 1", "serviceIds": [], "color": null },
            { "id": "b2", "name": "Box 2", "serviceIds": [], "color": null },
            { "id": "b3", "name": "Lavagem rápida", "serviceIds": ["s1", "s2"], "color": null }
          ],
          "hours": [null, [480, 1080], [480, 1080], [480, 1080], [480, 1080], [480, 1080], [480, 780]],
          "services": [
            { "id": "s1", "name": "Lavagem simples", "active": true, "color": null,
              "duration": { "Hatch": 60, "Sedan": 60, "SUV": 60, "Picape": 60, "Moto": 30 },
              "price": { "Hatch": 50, "Sedan": 60, "SUV": 70, "Picape": 80, "Moto": 30 } },
            { "id": "s2", "name": "Lavagem detalhada", "active": true, "color": null,
              "duration": { "Hatch": 60, "Sedan": 60, "SUV": 90, "Picape": 90, "Moto": 30 },
              "price": { "Hatch": 120, "Sedan": 140, "SUV": 160, "Picape": 180, "Moto": 70 } },
            { "id": "s3", "name": "Higienização interna", "active": true, "color": null,
              "duration": { "Hatch": 90, "Sedan": 90, "SUV": 120, "Picape": 120, "Moto": 60 },
              "price": { "Hatch": 250, "Sedan": 280, "SUV": 320, "Picape": 350 } },
            { "id": "s4", "name": "Polimento técnico", "active": true, "color": null,
              "duration": { "Hatch": 210, "Sedan": 210, "SUV": 240, "Picape": 240, "Moto": 180 },
              "price": { "Hatch": 450, "Sedan": 500, "SUV": 600, "Picape": 650, "Moto": 250 } },
            { "id": "s5", "name": "Vitrificação de pintura", "active": true, "color": null,
              "duration": { "Hatch": 240, "Sedan": 240, "SUV": 300, "Picape": 300, "Moto": 210 },
              "price": { "Hatch": 900, "Sedan": 1000, "SUV": 1200, "Picape": 1300, "Moto": 500 } },
            { "id": "s6", "name": "Cristalização de vidros", "active": true, "color": null,
              "duration": { "Hatch": 60, "Sedan": 60, "SUV": 60, "Picape": 60, "Moto": 30 },
              "price": { "Hatch": 150, "Sedan": 150, "SUV": 180, "Picape": 200 } }
          ],
          "reminder": { "sendAt": 600, "windowStart": 540, "windowEnd": 1140, "minLeadHours": 24, "mondayPolicy": "saturday" },
          "bot": { "daysAhead": 7, "sessionTimeoutMin": 30, "maxInvalidAttempts": 3, "minNoticeMin": 60, "cancelMinHours": 0, "askVehicle": true, "showPrices": true,
                   "maxUpcoming": 3, "noShowLimit": 2, "noShowDays": 180 }
        }
        """;
}

public sealed class BoxModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Serviços que o box atende. Vazio = todos.</summary>
    public List<string> ServiceIds { get; set; } = [];

    public bool Serves(string serviceId) => ServiceIds.Count == 0 || ServiceIds.Contains(serviceId);
}

public sealed class ServiceModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, int> Duration { get; set; } = [];
    public Dictionary<string, decimal> Price { get; set; } = [];
    public bool Active { get; set; } = true;

    public int DurationFor(string vehicleType) =>
        Duration.TryGetValue(vehicleType, out var d) && d > 0 ? d : 60;

    public decimal? PriceFor(string vehicleType) => Price.TryGetValue(vehicleType, out var p) ? p : null;
}

public sealed class ReminderPolicyModel
{
    public int SendAt { get; set; } = 600;
    public int WindowStart { get; set; } = 540;
    public int WindowEnd { get; set; } = 1140;
    public int MinLeadHours { get; set; } = 24;

    /// <summary>"saturday" ou "sunday": quando sai o lembrete de quem marcou na segunda.</summary>
    public string MondayPolicy { get; set; } = "saturday";

    /// <summary>
    /// Sem resposta ao lembrete: manda de novo este tanto de horas antes do horário (dentro da janela de envio). 0 = desligado.
    /// </summary>
    public int RetryHours { get; set; }
}

public sealed class BotSettingsModel
{
    public int DaysAhead { get; set; } = 7;
    public int SessionTimeoutMin { get; set; } = 30;
    public int MaxInvalidAttempts { get; set; } = 3;
    public int MinNoticeMin { get; set; } = 60;
    public int CancelMinHours { get; set; }
    public bool AskVehicle { get; set; } = true;
    public bool ShowPrices { get; set; } = true;

    /// <summary>Horários futuros que um número pode ter marcados pelo bot. 0 = sem limite.</summary>
    public int MaxUpcoming { get; set; } = 3;

    /// <summary>Faltas (nos últimos <see cref="NoShowDays"/> dias) a partir das quais o bot manda agendar com a equipe. 0 = desligado.</summary>
    public int NoShowLimit { get; set; } = 2;
    public int NoShowDays { get; set; } = 180;

    /// <summary>Oferece os adicionais ativos depois do veículo (até 3 por agendamento).</summary>
    public bool OfferExtras { get; set; } = true;
}

/// <summary>
/// Mensagens automáticas pelo WhatsApp (só para quem aceitou receber). Cada uma precisa de um modelo aprovado na Meta,
/// por isso começam desligadas.
/// </summary>
public sealed class NotifyModel
{
    /// <summary>Confirmação com o resumo logo depois de agendar pelo balcão (com Remarcar e Cancelar).</summary>
    public bool Booked { get; set; }

    /// <summary>"Seu carro está pronto" quando o atendimento é concluído.</summary>
    public bool Ready { get; set; }

    /// <summary>"Como foi o atendimento?" com Ótimo, Bom e Ruim, algumas horas depois de concluir.</summary>
    public bool Review { get; set; }
    public int ReviewDelayHours { get; set; } = 2;

    /// <summary>Lista de espera: o bot oferece "me avise se abrir vaga" nos dias lotados e o cliente é avisado quando abre.</summary>
    public bool Waitlist { get; set; }
}

/// <summary>Mensalidades dos planos de fidelidade.</summary>
public sealed class PlanBillingModel
{
    /// <summary>Dias depois do vencimento até a mensalidade contar como atrasada.</summary>
    public int GraceDays { get; set; } = 5;

    /// <summary>Com mensalidade atrasada o plano para de cobrir agendamentos novos (volta ao pagar).</summary>
    public bool SuspendOverdue { get; set; }

    /// <summary>Aviso pelo WhatsApp alguns dias antes do vencimento (modelo da Meta).</summary>
    public bool NotifyDue { get; set; }
    public int NotifyDaysBefore { get; set; } = 3;

    /// <summary>Aviso pelo WhatsApp quando atrasa (uma vez por mês; modelo da Meta).</summary>
    public bool NotifyOverdue { get; set; }
}

/// <summary>Bloqueio semanal. <c>Days</c>: 0 = domingo … 6 = sábado. <c>BoxId</c> nulo = todos os boxes.</summary>
public sealed class RecurringBlockModel
{
    public List<int> Days { get; set; } = [];
    public int Start { get; set; }
    public int End { get; set; }
    public string? BoxId { get; set; }
    public string Reason { get; set; } = string.Empty;

    public bool AppliesTo(DateTime date) => End > Start && Days.Contains((int)date.DayOfWeek);
}

public sealed class HolidayPolicyModel
{
    /// <summary>Fecha nos feriados nacionais (fixos e Sexta-feira Santa).</summary>
    public bool National { get; set; }

    /// <summary>Fecha também no Carnaval (segunda e terça) e no Corpus Christi.</summary>
    public bool Optional { get; set; }

    /// <summary>Datas em que fecha o dia todo (feriado municipal, folga coletiva…).</summary>
    public List<HolidayModel> Dates { get; set; } = [];
}

public sealed class HolidayModel
{
    /// <summary>'AAAA-MM-DD'.</summary>
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>Adicional do serviço. Sem duração para o tipo = não acrescenta tempo; sem preço = sai de graça.</summary>
public sealed class ExtraModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, int> Duration { get; set; } = [];
    public Dictionary<string, decimal> Price { get; set; } = [];
    public bool Active { get; set; } = true;

    public int DurationFor(string vehicleType) => Duration.TryGetValue(vehicleType, out var d) && d > 0 ? d : 0;
    public decimal? PriceFor(string vehicleType) => Price.TryGetValue(vehicleType, out var p) ? p : null;
}

/// <summary>Informações da estética para o bot. Vazio = não mostra a linha.</summary>
public sealed class BusinessInfoModel
{
    public string? Address { get; set; }
    public string? MapsUrl { get; set; }
    public string? Payment { get; set; }
    public string? Notes { get; set; }
}

public sealed class TermsModel
{
    public string BoxSingular { get; set; } = "Box";
    public string BoxPlural { get; set; } = "Boxes";

    /// <summary>Nomes de status trocados em Ajustes (chave: scheduled, inProgress, done, late, canceled, noShow).</summary>
    public Dictionary<string, string>? StatusLabels { get; set; }
}

public sealed class VehicleTypeConfigModel
{
    public string? Label { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>Status do agendamento como texto (espelha Domain.Agenda.BookingStatuses).</summary>
internal static class BookingStatusKeys
{
    public const string InProgress = "in_progress";
    public const string NoShow = "no_show";
}

/// <summary>Textos do bot. Em branco no JSON = texto padrão (mesmos do app).</summary>
public sealed class BotTextsModel
{
    public const string DefaultGreeting = "Olá, {nome}! 👋 Sou o assistente de agendamento da *{estetica}*.";
    public const string DefaultBooked = "Agendado! ✅\n🚗 {servico}\n📅 {data}, das {hora} às {fim}";
    public const string DefaultConfirmed = "Confirmado, te esperamos! 🚗✨\n📅 {data} às {hora}\n🧽 {servico}";
    public const string DefaultAttendant = "Certo! Já avisei a equipe, alguém continua a conversa por aqui em instantes. 🙋\nPara voltar ao agendamento automático, digite *menu*.";
    public const string DefaultReminder = "Olá {nome}! Lembrete do seu horário:\n📅 {data} às {hora}\n🚗 {servico}\n\nVocê confirma?";
    public const string DefaultCounterBooked = "Olá {nome}! Seu horário na {estetica} está marcado ✅\n📅 {data} às {hora}\n🚗 {servico}";
    public const string DefaultReady = "Olá {nome}! Seu veículo está pronto ✨\n🚗 {servico}\nPode vir buscar quando quiser. Obrigado pela preferência!";
    public const string DefaultPlanDue = "Olá {nome}! A mensalidade do seu plano {plano} ({valor}) vence em {vencimento}.\n{pagamento}";
    public const string DefaultPlanOverdue =
        "Olá {nome}! A mensalidade do seu plano {plano} ({valor}) venceu em {vencimento} e ainda não consta como paga.\n{pagamento}\nSe já pagou, desconsidere 🙏";

    public const string DefaultWaitlist =
        "Olá {nome}! Abriu horário na {estetica} em {data} para {servico} 🎉\nO primeiro livre é às {hora}. Quer agendar? Toque em *Ver horários*.";

    /// <summary>Aviso de vaga da lista de espera (modelo da Meta "vaga_disponivel", botão Ver horários).</summary>
    public string? Waitlist { get; set; }

    public string WaitlistText => Or(Waitlist, DefaultWaitlist);

    public const string DefaultReview = "Olá {nome}! Como foi o atendimento hoje na {estetica}? 🚗✨\nSua opinião ajuda a gente a melhorar.";

    /// <summary>Pedido de avaliação depois do atendimento (modelo da Meta, com Ótimo, Bom e Ruim).</summary>
    public string? Review { get; set; }

    public string ReviewText => Or(Review, DefaultReview);

    /// <summary>Aviso de mensalidade vencendo (modelo da Meta).</summary>
    public string? PlanDue { get; set; }

    /// <summary>Aviso de mensalidade atrasada (modelo da Meta).</summary>
    public string? PlanOverdue { get; set; }

    public string? Greeting { get; set; }
    public string? Booked { get; set; }
    public string? Confirmed { get; set; }
    public string? Attendant { get; set; }
    public string? Reminder { get; set; }

    /// <summary>Confirmação do agendamento feito no balcão (modelo da Meta).</summary>
    public string? CounterBooked { get; set; }

    /// <summary>"Seu carro está pronto" (modelo da Meta).</summary>
    public string? Ready { get; set; }

    private static string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    public string GreetingText => Or(Greeting, DefaultGreeting);
    public string BookedText => Or(Booked, DefaultBooked);
    public string ConfirmedText => Or(Confirmed, DefaultConfirmed);
    public string AttendantText => Or(Attendant, DefaultAttendant);
    public string ReminderText => Or(Reminder, DefaultReminder);
    public string CounterBookedText => Or(CounterBooked, DefaultCounterBooked);
    public string ReadyText => Or(Ready, DefaultReady);
    public string PlanDueText => Or(PlanDue, DefaultPlanDue);
    public string PlanOverdueText => Or(PlanOverdue, DefaultPlanOverdue);
}
