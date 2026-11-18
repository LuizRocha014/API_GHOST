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

    public string? Greeting { get; set; }
    public string? Booked { get; set; }
    public string? Confirmed { get; set; }
    public string? Attendant { get; set; }
    public string? Reminder { get; set; }

    private static string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    public string GreetingText => Or(Greeting, DefaultGreeting);
    public string BookedText => Or(Booked, DefaultBooked);
    public string ConfirmedText => Or(Confirmed, DefaultConfirmed);
    public string AttendantText => Or(Attendant, DefaultAttendant);
    public string ReminderText => Or(Reminder, DefaultReminder);
}
