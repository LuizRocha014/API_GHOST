using System.Net;
using System.Text;
using System.Text.Json;
using Application.Abstractions;
using Application.Agenda;
using Application.Bot;
using Domain.Agenda;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.WhatsApp;

/// <summary>
/// Envio pela WhatsApp Cloud API: <c>POST https://graph.facebook.com/{versão}/{phone-number-id}/messages</c>.
/// O número de origem é o da filial (<see cref="WhatsAppOptions.Numbers"/>). Devolve o wamid.
/// </summary>
public sealed class CloudMessagingChannel(IOptions<WhatsAppOptions> options, ILogger<CloudMessagingChannel> logger)
    : IMessagingChannel, IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>Esperas entre as tentativas quando a Meta limita a taxa ou falha do lado dela.</summary>
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)];

    /// <summary>Códigos da Graph API de limite de taxa / falha temporária (vale tentar de novo em instantes).</summary>
    private static readonly HashSet<string> TransientCodes = ["1", "2", "4", "17", "80007", "130429", "131000", "133004"];

    public async Task<string> SendAsync(Guid branchId, string phone, OutgoingMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        var numberId = o.PhoneNumberIdFor(branchId)
                       ?? throw new WhatsAppException($"A filial {branchId} não tem número do WhatsApp em WhatsApp:Numbers.");
        if (string.IsNullOrWhiteSpace(o.AccessToken)) throw new WhatsAppException("WhatsApp:AccessToken não configurado.");

        var to = AgendaFormat.NormalizePhone(phone);
        var payload = CloudPayloads.Build(to, message).ToJsonString();
        for (var attempt = 0; ; attempt++)
        {
            string body;
            HttpStatusCode status;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{o.GraphVersion}/{numberId}/messages")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new("Bearer", o.AccessToken);
                using var response = await _http.SendAsync(request, ct);
                body = await response.Content.ReadAsStringAsync(ct);
                status = response.StatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // Rede caiu ou a Meta não respondeu a tempo.
                if (attempt < RetryDelays.Length)
                {
                    logger.LogWarning("WhatsApp: falha de rede no envio para {Phone}; tento de novo ({Attempt}).", to, attempt + 2);
                    await Task.Delay(RetryDelays[attempt], ct);
                    continue;
                }
                throw new WhatsAppException($"Sem resposta da Meta: {ex.Message}");
            }

            if ((int)status is >= 200 and < 300)
            {
                var wamId = ReadWamId(body) ?? throw new WhatsAppException("A Cloud API aceitou o envio mas não devolveu o id da mensagem.");
                logger.LogInformation("WhatsApp: {Kind} enviado para {Phone} ({WamId})", message.Kind, to, wamId);
                return wamId;
            }

            var error = DescribeError(status, body);
            if (IsTransient(status, body) && attempt < RetryDelays.Length)
            {
                logger.LogWarning("WhatsApp: envio para {Phone} recusado por enquanto ({Error}); tento de novo.", to, error);
                await Task.Delay(RetryDelays[attempt], ct);
                continue;
            }
            logger.LogWarning("WhatsApp: envio {Kind} para {Phone} recusado: {Error}", message.Kind, to, error);
            throw new WhatsAppException(error);
        }
    }

    private static bool IsTransient(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.TooManyRequests || (int)status >= 500) return true;
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var e)
                   && ((e.TryGetProperty("code", out var c) && TransientCodes.Contains(c.ToString()))
                       || (e.TryGetProperty("is_transient", out var t) && t.ValueKind == JsonValueKind.True));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadWamId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array
                   && messages.GetArrayLength() > 0 && messages[0].TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"(#131030) Recipient phone number not in allowed list" e afins.</summary>
    private static string DescribeError(HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e))
            {
                var code = e.TryGetProperty("code", out var c) ? c.ToString() : "?";
                var msg = e.TryGetProperty("message", out var m) ? m.GetString() : null;
                var details = e.TryGetProperty("error_data", out var d) && d.TryGetProperty("details", out var det) ? det.GetString() : null;
                return $"(#{code}) {msg}{(string.IsNullOrEmpty(details) ? string.Empty : " – " + details)}";
            }
        }
        catch (JsonException)
        {
        }
        return $"HTTP {(int)status}";
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Modo Log: nada sai para a Meta; o envio fica no log e devolve um id local.</summary>
public sealed class LogMessagingChannel(ILogger<LogMessagingChannel> logger) : IMessagingChannel
{
    public Task<string> SendAsync(Guid branchId, string phone, OutgoingMessage message, CancellationToken ct = default)
    {
        var wamId = $"wamid.local.{Guid.NewGuid():N}";
        logger.LogInformation("WhatsApp (modo Log) {Kind} para {Phone} ({WamId}):\n{Body}", message.Kind, phone, wamId, message.Body);
        return Task.FromResult(wamId);
    }
}

/// <summary>Aviso de mensalidade pelo modelo aprovado; fica registrado na conversa do cliente.</summary>
public sealed class WhatsAppPlanNoticeSender(
    IMessagingChannel channel,
    IBotStore store,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppPlanNoticeSender> logger) : IPlanNoticeSender
{
    public async Task<string> SendAsync(
        string type, Guid branchId, string phone, IReadOnlyDictionary<string, string> values, AgendaConfigModel config, CancellationToken ct = default)
    {
        var o = options.Value;
        var spec = type == PlanNoticeTypes.Overdue ? BotTemplates.PlanOverdueSpec : BotTemplates.PlanDueSpec;
        var message = BotTemplates.BuildPlan(spec, values, config, o.TemplateNameFor(spec), o.TemplateLanguage);
        var to = AgendaFormat.NormalizePhone(phone);
        var wamId = await channel.SendAsync(branchId, to, message, ct);
        try
        {
            await store.LogMessageAsync(branchId, to, "out", message.Kind, message.Body, wamId, new BotLogExtra(BotDtoMapping.MetaJson(message)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Aviso de mensalidade {WamId} enviado, mas não foi registrado na conversa.", wamId);
        }
        return wamId;
    }
}

/// <summary>Aviso de vaga da lista de espera pelo modelo aprovado (botão Ver horários = wl:{id}).</summary>
public sealed class WhatsAppWaitlistNoticeSender(
    IMessagingChannel channel,
    IBotStore store,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppWaitlistNoticeSender> logger) : IWaitlistNoticeSender
{
    public async Task<string> SendAsync(
        Guid branchId, AgendaWaitlistEntry entry, IReadOnlyDictionary<string, string> values, AgendaConfigModel config, CancellationToken ct = default)
    {
        var o = options.Value;
        var message = BotTemplates.Waitlist(entry.Id, values, config, o.TemplateNameFor(BotTemplates.WaitlistSpec), o.TemplateLanguage);
        var to = AgendaFormat.NormalizePhone(entry.Phone);
        var wamId = await channel.SendAsync(branchId, to, message, ct);
        try
        {
            await store.LogMessageAsync(branchId, to, "out", message.Kind, message.Body, wamId, new BotLogExtra(BotDtoMapping.MetaJson(message)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Aviso de vaga {WamId} enviado, mas não foi registrado na conversa.", wamId);
        }
        return wamId;
    }
}

/// <summary>
/// Lembrete D-1 pelo template aprovado (fora da janela de 24 h só template pode ser enviado).
/// O texto e a ordem dos parâmetros saem de Ajustes; o nome do template, de WhatsApp:ReminderTemplate.
/// </summary>
public sealed class WhatsAppReminderSender(
    IMessagingChannel channel,
    IBotStore store,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppReminderSender> logger) : IReminderSender
{
    public async Task<string> SendAsync(string type, AgendaBooking booking, AgendaConfigModel config, CancellationToken ct = default)
    {
        var o = options.Value;
        var spec = BotTemplates.For(type);
        var message = BotTemplates.Build(spec, booking.ToDto(), config, o.TemplateNameFor(spec), o.TemplateLanguage);
        var phone = AgendaFormat.NormalizePhone(booking.Phone);
        var wamId = await channel.SendAsync(booking.BranchId, phone, message, ct);
        try
        {
            await store.LogMessageAsync(booking.BranchId, phone, "out", message.Kind, message.Body, wamId, new BotLogExtra(BotDtoMapping.MetaJson(message)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O lembrete já saiu: falhar aqui faria reenviar.
            logger.LogWarning(ex, "Lembrete {WamId} enviado, mas não foi registrado na conversa.", wamId);
        }
        return wamId;
    }
}
