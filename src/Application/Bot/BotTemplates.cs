using Application.Agenda;

namespace Application.Bot;

/// <summary>
/// Template do lembrete D-1 (categoria Utility, aprovado no WhatsApp Manager). O texto vem de Ajustes
/// (<see cref="BotTextsModel.ReminderText"/>) com variáveis entre chaves; na Meta o modelo usa posições
/// ({{1}}, {{2}}…) na ordem em que as variáveis aparecem. Botões de resposta rápida: conf:{id}, rem:{id}, canc:{id}.
/// </summary>
public static class BotTemplates
{
    public const string ReminderName = "lembrete_confirmacao";
    public const string DefaultLanguage = "pt_BR";

    public static readonly IReadOnlyList<string> ReminderButtons = ["Confirmar", "Remarcar", "Cancelar"];

    public static TemplateMessage Reminder(BookingDto b, AgendaConfigModel cfg, string name = ReminderName, string language = DefaultLanguage)
    {
        var text = cfg.Texts.ReminderText;
        var values = BotFormat.BookingValues(b, cfg.BusinessName, cfg.BoxName(b.BoxId));
        return new TemplateMessage(
            name,
            language,
            BotFormat.Variables(text).Select(v => Param(values.GetValueOrDefault(v))).ToList(),
            [
                new TemplateButton($"conf:{b.Id}", ReminderButtons[0]),
                new TemplateButton($"rem:{b.Id}", ReminderButtons[1]),
                new TemplateButton($"canc:{b.Id}", ReminderButtons[2]),
            ],
            BotFormat.Fill(text, values));
    }

    /// <summary>Texto no formato a cadastrar na Meta: cada variável vira {{1}}, {{2}}… na ordem em que aparece.</summary>
    public static string MetaBody(string text)
    {
        var order = BotFormat.Variables(text);
        return BotFormat.Fill(text, order.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => $"{{{{{x.i + 1}}}}}"));
    }

    /// <summary>
    /// A Meta recusa parâmetro vazio, com quebra de linha, tabulação ou mais de 4 espaços seguidos.
    /// </summary>
    private static string Param(string? value)
    {
        var v = (value ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
        while (v.Contains("     ", StringComparison.Ordinal)) v = v.Replace("     ", " ", StringComparison.Ordinal);
        return v.Length == 0 ? "-" : v;
    }
}

/// <summary>Canal que só guarda o que o bot respondeu (simulação pela API e testes).</summary>
public sealed class CaptureMessagingChannel : IMessagingChannel
{
    private readonly List<OutgoingMessage> _sent = [];

    public IReadOnlyList<OutgoingMessage> Sent => _sent;

    public Task<string> SendAsync(Guid branchId, string phone, OutgoingMessage message, CancellationToken ct = default)
    {
        _sent.Add(message);
        return Task.FromResult($"wamid.sim.{Guid.NewGuid():N}");
    }
}
