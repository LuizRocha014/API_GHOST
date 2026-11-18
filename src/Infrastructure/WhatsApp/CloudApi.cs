using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Bot;

namespace Infrastructure.WhatsApp;

/// <summary>Corpo do <c>POST /{phone-number-id}/messages</c> da Cloud API (mesmo formato do simulador do app).</summary>
public static class CloudPayloads
{
    private const int HeaderMax = 60;
    private const int FooterMax = 60;
    private const int SectionTitleMax = 24;

    public static JsonObject Build(string to, OutgoingMessage message)
    {
        var o = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = to,
        };
        switch (message)
        {
            case TextMessage t:
                o["type"] = "text";
                o["text"] = new JsonObject { ["preview_url"] = false, ["body"] = Cut(t.Text, CloudLimits.Text) };
                break;

            case ListMessage l:
            {
                var interactive = new JsonObject { ["type"] = "list" };
                if (l.Header is not null) interactive["header"] = new JsonObject { ["type"] = "text", ["text"] = Cut(l.Header, HeaderMax) };
                interactive["body"] = new JsonObject { ["text"] = Cut(l.Text, CloudLimits.Body) };
                if (l.Footer is not null) interactive["footer"] = new JsonObject { ["text"] = Cut(l.Footer, FooterMax) };
                var sections = new JsonArray();
                foreach (var s in l.Sections)
                {
                    var section = new JsonObject();
                    if (s.Title is not null) section["title"] = Cut(s.Title, SectionTitleMax);
                    section["rows"] = new JsonArray(s.Rows.Select(Row).ToArray());
                    sections.Add(section);
                }
                interactive["action"] = new JsonObject { ["button"] = Cut(l.Button, CloudLimits.ListButton), ["sections"] = sections };
                o["type"] = "interactive";
                o["interactive"] = interactive;
                break;
            }

            case ButtonsMessage b:
            {
                var interactive = new JsonObject
                {
                    ["type"] = "button",
                    ["body"] = new JsonObject { ["text"] = Cut(b.Text, CloudLimits.Body) },
                };
                if (b.Footer is not null) interactive["footer"] = new JsonObject { ["text"] = Cut(b.Footer, FooterMax) };
                interactive["action"] = new JsonObject
                {
                    ["buttons"] = new JsonArray(b.Buttons.Take(CloudLimits.ReplyButtons).Select(x => (JsonNode?)new JsonObject
                    {
                        ["type"] = "reply",
                        ["reply"] = new JsonObject { ["id"] = x.Id, ["title"] = Cut(x.Title, CloudLimits.ReplyButtonTitle) },
                    }).ToArray()),
                };
                o["type"] = "interactive";
                o["interactive"] = interactive;
                break;
            }

            case TemplateMessage t:
            {
                var components = new JsonArray();
                if (t.BodyParams.Count > 0)
                {
                    components.Add(new JsonObject
                    {
                        ["type"] = "body",
                        ["parameters"] = new JsonArray(t.BodyParams.Select(p => (JsonNode?)new JsonObject { ["type"] = "text", ["text"] = p }).ToArray()),
                    });
                }
                for (var i = 0; i < t.Buttons.Count; i++)
                {
                    components.Add(new JsonObject
                    {
                        ["type"] = "button",
                        ["sub_type"] = "quick_reply",
                        ["index"] = i.ToString(CultureInfo.InvariantCulture),
                        ["parameters"] = new JsonArray(new JsonObject { ["type"] = "payload", ["payload"] = t.Buttons[i].Payload }),
                    });
                }
                o["type"] = "template";
                o["template"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["language"] = new JsonObject { ["code"] = t.Language },
                    ["components"] = components,
                };
                break;
            }

            default:
                throw new NotSupportedException($"Mensagem {message.GetType().Name} sem formato na Cloud API.");
        }
        return o;
    }

    private static JsonNode? Row(BotOption r)
    {
        var row = new JsonObject { ["id"] = r.Id, ["title"] = Cut(r.Title, CloudLimits.ListRowTitle) };
        if (!string.IsNullOrEmpty(r.Description)) row["description"] = Cut(r.Description, CloudLimits.ListRowDescription);
        return row;
    }

    private static string Cut(string s, int max) => BotFormat.Truncate(s, max);
}

/// <summary>Mensagem recebida no webhook e o número (phone_number_id) que a recebeu: é ele que diz a filial.</summary>
public sealed record InboundMessage(string PhoneNumberId, IncomingMessage Message);

public sealed record InboundStatus(string PhoneNumberId, DeliveryStatus Status);

public sealed record ParsedWebhook(IReadOnlyList<InboundMessage> Messages, IReadOnlyList<InboundStatus> Statuses);

/// <summary>Lê o corpo que a Meta manda no webhook: mensagens do cliente e status de entrega.</summary>
public static class WebhookParser
{
    /// <summary>Tipos que não pedem resposta (reação a uma mensagem, aviso de troca de número).</summary>
    private static readonly HashSet<string> Ignored = ["reaction", "system", "ephemeral"];

    public static ParsedWebhook Parse(JsonElement root)
    {
        var messages = new List<InboundMessage>();
        var statuses = new List<InboundStatus>();
        if (root.ValueKind != JsonValueKind.Object || Str(root, "object") != "whatsapp_business_account")
            return new ParsedWebhook(messages, statuses);

        foreach (var entry in Arr(root, "entry"))
        foreach (var change in Arr(entry, "changes"))
        {
            if (Str(change, "field") != "messages" || !Obj(change, "value", out var v)) continue;
            var phoneNumberId = Obj(v, "metadata", out var metadata) ? Str(metadata, "phone_number_id") ?? string.Empty : string.Empty;

            var names = new Dictionary<string, string?>();
            foreach (var contact in Arr(v, "contacts"))
            {
                var waId = Str(contact, "wa_id");
                if (waId is not null) names[waId] = Obj(contact, "profile", out var profile) ? Str(profile, "name") : null;
            }

            foreach (var m in Arr(v, "messages"))
            {
                var id = Str(m, "id");
                var from = Str(m, "from");
                var type = Str(m, "type") ?? "unknown";
                if (id is null || from is null || Ignored.Contains(type)) continue;
                string? text = null, selectionId = null, selectionTitle = null;
                switch (type)
                {
                    case "text":
                        text = Obj(m, "text", out var t) ? Str(t, "body") : null;
                        break;
                    case "interactive":
                        if (Obj(m, "interactive", out var i) && (Obj(i, "list_reply", out var reply) || Obj(i, "button_reply", out reply)))
                        {
                            selectionId = Str(reply, "id");
                            selectionTitle = Str(reply, "title");
                        }
                        break;
                    case "button":
                        // Botão de resposta rápida do template (ex.: lembrete D-1).
                        if (Obj(m, "button", out var b))
                        {
                            selectionId = Str(b, "payload");
                            selectionTitle = Str(b, "text");
                        }
                        break;
                }
                messages.Add(new InboundMessage(phoneNumberId, new IncomingMessage(
                    id, from, Timestamp(Str(m, "timestamp")), type, text, selectionId, selectionTitle, names.GetValueOrDefault(from))));
            }

            foreach (var s in Arr(v, "statuses"))
            {
                var wamId = Str(s, "id");
                if (wamId is null) continue;
                string? error = null;
                foreach (var e in Arr(s, "errors"))
                {
                    var code = e.TryGetProperty("code", out var c) ? c.ToString() : "?";
                    error = $"(#{code}) {Str(e, "message") ?? Str(e, "title")}";
                    break;
                }
                statuses.Add(new InboundStatus(phoneNumberId, new DeliveryStatus(
                    wamId, Str(s, "recipient_id") ?? string.Empty, Str(s, "status") ?? string.Empty, Timestamp(Str(s, "timestamp")), error)));
            }
        }
        return new ParsedWebhook(messages, statuses);
    }

    private static DateTime Timestamp(string? unix) =>
        long.TryParse(unix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
            : DateTime.UtcNow;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Obj(JsonElement e, string name, out JsonElement value)
    {
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object) return true;
        value = default;
        return false;
    }

    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray()
            : [];
}

/// <summary>Assinatura do webhook: <c>X-Hub-Signature-256: sha256=HMAC-SHA256(corpo, AppSecret)</c> em hexadecimal.</summary>
public static class WebhookSignature
{
    public const string Header = "X-Hub-Signature-256";

    public static bool IsValid(ReadOnlySpan<byte> body, string? header, string appSecret)
    {
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(header) || !header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;
        byte[] given;
        try
        {
            given = Convert.FromHexString(header.AsSpan(7));
        }
        catch (FormatException)
        {
            return false;
        }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>Compara o hub.verify_token sem vazar o tamanho do acerto pelo tempo.</summary>
    public static bool SameToken(string? given, string expected) =>
        !string.IsNullOrEmpty(expected) && given is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
}
