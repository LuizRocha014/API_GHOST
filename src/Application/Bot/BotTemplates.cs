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
    public const string BookedName = "agendamento_confirmado";
    public const string ReadyName = "carro_pronto";
    public const string DefaultLanguage = "pt_BR";

    public static readonly IReadOnlyList<string> ReminderButtons = ["Confirmar", "Remarcar", "Cancelar"];

    /// <summary>
    /// Um modelo da Meta: o tipo da fila que ele atende, o nome padrão, o texto (de Ajustes) e os botões de resposta
    /// rápida (prefixo do id que volta no webhook + título).
    /// </summary>
    public sealed record Spec(
        string Type, string DefaultName, string Title, Func<BotTextsModel, string> Text, IReadOnlyList<(string Prefix, string Title)> Buttons);

    public static readonly Spec ReminderSpec = new(
        Domain.Agenda.ReminderTypes.ConfirmationD1, ReminderName, "Lembrete na véspera", t => t.ReminderText,
        [("conf", ReminderButtons[0]), ("rem", ReminderButtons[1]), ("canc", ReminderButtons[2])]);

    public static readonly Spec BookedSpec = new(
        Domain.Agenda.ReminderTypes.Booked, BookedName, "Confirmação do agendamento feito no balcão", t => t.CounterBookedText,
        [("rem", "Remarcar"), ("canc", "Cancelar")]);

    public static readonly Spec ReadySpec = new(
        Domain.Agenda.ReminderTypes.Ready, ReadyName, "Carro pronto", t => t.ReadyText, []);

    public static readonly IReadOnlyList<Spec> All = [ReminderSpec, BookedSpec, ReadySpec];

    public static Spec For(string type) => All.FirstOrDefault(s => s.Type == type) ?? ReminderSpec;

    public static TemplateMessage Build(Spec spec, BookingDto b, AgendaConfigModel cfg, string? name = null, string language = DefaultLanguage)
    {
        var text = spec.Text(cfg.Texts);
        var values = BotFormat.BookingValues(b, cfg.BusinessName, cfg.BoxName(b.BoxId));
        return new TemplateMessage(
            string.IsNullOrWhiteSpace(name) ? spec.DefaultName : name,
            language,
            BotFormat.Variables(text).Select(v => Param(values.GetValueOrDefault(v))).ToList(),
            spec.Buttons.Select(x => new TemplateButton($"{x.Prefix}:{b.Id}", x.Title)).ToList(),
            BotFormat.Fill(text, values));
    }

    public static TemplateMessage Reminder(BookingDto b, AgendaConfigModel cfg, string name = ReminderName, string language = DefaultLanguage) =>
        Build(ReminderSpec, b, cfg, name, language);

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
