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
          "bot": { "daysAhead": 7, "sessionTimeoutMin": 30, "maxInvalidAttempts": 3, "minNoticeMin": 60, "cancelMinHours": 0, "askVehicle": true, "showPrices": true }
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
    public int MinNoticeMin { get; set; } = 60;
    public int CancelMinHours { get; set; }
}

public sealed class TermsModel
{
    public string BoxSingular { get; set; } = "Box";
    public string BoxPlural { get; set; } = "Boxes";
}
