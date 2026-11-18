using Application.Abstractions;
using Application.Agenda;
using Microsoft.Extensions.Logging;

namespace Application.Bot;

/// <summary>
/// Caixa de entrada da equipe: conversas do WhatsApp, quem pediu atendente, responder (dentro da janela de 24 h da
/// Meta), assumir a conversa e devolver ao bot.
/// </summary>
public sealed class BotInboxService(IBotStore store, IMessagingChannel channel, IAgendaClock clock, ILogger<BotInboxService> logger)
{
    /// <summary>Fora disso a Meta só aceita template: resposta livre da equipe é recusada.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private static readonly TimeSpan ListSince = TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<BotConversationSummaryDto>> ListAsync(Guid branchId, int limit, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var rows = await store.ListConversationsAsync(branchId, nowUtc - ListSince, Math.Clamp(limit, 1, 200), ct);
        var names = await NamesAsync(branchId, rows.Select(r => r.Phone), ct);
        return rows.Select(r =>
        {
            var session = string.IsNullOrEmpty(r.DataJson) ? null : BotSession.FromJson(r.DataJson, r.Phone);
            var state = r.State ?? BotStates.Idle;
            var client = names.GetValueOrDefault(r.Phone);
            var name = client?.Name is { Length: > 0 } n ? n : session?.ProfileName is { Length: > 0 } p ? p : r.Phone;
            var last = new BotLogEntry(r.Phone, r.LastDirection, r.LastKind, r.LastBody, null, r.LastAt, null, null).ToDto();
            return new BotConversationSummaryDto(
                r.Phone, name, client?.Id, state, state == BotStates.WithAttendant, r.Unread, WindowOpen(r.LastInAt, nowUtc), last);
        }).ToList();
    }

    public async Task<BotConversationDto> GetAsync(Guid branchId, string phone, int limit, CancellationToken ct = default)
    {
        var phones = AgendaFormat.PhoneVariants(phone);
        var messages = await store.ListMessagesAsync(branchId, phones, limit, ct);
        var waId = messages.LastOrDefault(m => m.Direction == "in")?.Phone ?? messages.LastOrDefault()?.Phone ?? phones[0];
        var session = await SessionAsync(branchId, phones, ct);
        var client = (await NamesAsync(branchId, [waId], ct)).GetValueOrDefault(waId);
        var lastIn = messages.LastOrDefault(m => m.Direction == "in")?.CreatedAt;
        var nowUtc = DateTime.UtcNow;
        var name = client?.Name is { Length: > 0 } n ? n : session?.ProfileName;
        return new BotConversationDto(
            session?.State ?? BotStates.Idle,
            messages.Select(m => m.ToDto()).ToList(),
            waId,
            name,
            client?.Id,
            WindowOpen(lastIn, nowUtc),
            lastIn is null ? null : DateTime.SpecifyKind(lastIn.Value + Window, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A equipe responde: manda pelo WhatsApp, guarda com o autor e deixa a conversa com a equipe (o bot fica calado).</summary>
    public async Task<BotLogDto> ReplyAsync(Guid branchId, string phone, string text, string author, CancellationToken ct = default)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0) throw new AgendaValidationException("Escreva a mensagem.");
        if (body.Length > CloudLimits.Text) throw new AgendaValidationException($"A mensagem pode ter no máximo {CloudLimits.Text} caracteres.");

        var phones = AgendaFormat.PhoneVariants(phone);
        var messages = await store.ListMessagesAsync(branchId, phones, 50, ct);
        var lastIn = messages.LastOrDefault(m => m.Direction == "in");
        if (lastIn is null || !WindowOpen(lastIn.CreatedAt, DateTime.UtcNow))
            throw new AgendaConflictException(
                "A janela de 24 horas do WhatsApp está fechada: o cliente não escreve há mais de 24 h, e fora dela a Meta só aceita " +
                "mensagens de modelo aprovado. Peça para o cliente mandar uma mensagem (ou ligue).");

        var waId = lastIn.Phone;
        string wamId;
        try
        {
            wamId = await channel.SendAsync(branchId, waId, new TextMessage(body), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Caixa de entrada: envio para {Phone} recusado", waId);
            throw new AgendaValidationException($"O WhatsApp não aceitou a mensagem: {ex.Message}");
        }
        var who = Truncate(string.IsNullOrWhiteSpace(author) ? "Equipe" : author.Trim(), 80);
        await store.LogMessageAsync(branchId, waId, "agent", "text", body, wamId, new BotLogExtra(Author: who), ct);
        await SetStateAsync(branchId, waId, human: true, ct);
        await store.MarkSeenAsync(branchId, waId, DateTime.UtcNow, ct);
        logger.LogInformation("Caixa de entrada: {Author} respondeu {Phone}", who, waId);
        return new BotLogEntry(waId, "agent", "text", body, wamId, DateTime.UtcNow, null, who).ToDto();
    }

    /// <summary>
    /// <paramref name="human"/> = a equipe assume (o bot para de responder por até 12 h ou até o cliente mandar "menu");
    /// falso = devolve ao bot, que responde a próxima mensagem do começo.
    /// </summary>
    public async Task<string> SetStateAsync(Guid branchId, string phone, bool human, CancellationToken ct = default)
    {
        var phones = AgendaFormat.PhoneVariants(phone);
        var session = await SessionAsync(branchId, phones, ct);
        if (session is null)
        {
            var messages = await store.ListMessagesAsync(branchId, phones, 1, ct);
            session = new BotSession { Phone = messages.LastOrDefault()?.Phone ?? phones[0] };
        }
        session.Reset();
        if (human) session.State = BotStates.WithAttendant;
        session.UpdatedAt = clock.NowLocal;
        await store.SaveSessionAsync(branchId, session, ct);
        return session.State;
    }

    public Task MarkSeenAsync(Guid branchId, string phone, CancellationToken ct = default) =>
        MarkSeenAnyAsync(branchId, AgendaFormat.PhoneVariants(phone), ct);

    private async Task MarkSeenAnyAsync(Guid branchId, IReadOnlyList<string> phones, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var p in phones) await store.MarkSeenAsync(branchId, p, now, ct);
    }

    private async Task<BotSession?> SessionAsync(Guid branchId, IReadOnlyList<string> phones, CancellationToken ct)
    {
        foreach (var p in phones)
        {
            if (await store.GetSessionAsync(branchId, p, ct) is { } s) return s;
        }
        return null;
    }

    /// <summary>Cliente cadastrado de cada telefone (com ou sem o nono dígito).</summary>
    private async Task<Dictionary<string, BotClientName>> NamesAsync(Guid branchId, IEnumerable<string> phones, CancellationToken ct)
    {
        var wanted = phones.Distinct().ToList();
        var variants = wanted.SelectMany(AgendaFormat.PhoneVariants).Distinct().ToList();
        if (variants.Count == 0) return [];
        var clients = await store.ClientNamesAsync(branchId, variants, ct);
        var result = new Dictionary<string, BotClientName>();
        foreach (var phone in wanted)
        {
            var mine = AgendaFormat.PhoneVariants(phone);
            var c = clients.FirstOrDefault(x => mine.Contains(x.Phone));
            if (c is not null) result[phone] = c;
        }
        return result;
    }

    private static bool WindowOpen(DateTime? lastInUtc, DateTime nowUtc) => lastInUtc is { } t && nowUtc - t < Window;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
