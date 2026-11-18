using System.Text.Json;
using System.Threading.Channels;
using Application.Agenda;
using Application.Bot;
using Domain.Agenda;
using Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace Api.Agenda;

/// <summary>
/// Campainha entre o webhook e o worker: o webhook grava na fila do banco e toca; o worker acorda na hora (sem ela,
/// confere a fila a cada poucos segundos de qualquer jeito).
/// </summary>
public sealed class BotSignal
{
    private readonly Channel<bool> _bell = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
    });

    public void Ring() => _bell.Writer.TryWrite(true);

    /// <summary>Espera o toque ou o <paramref name="timeout"/>.</summary>
    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await _bell.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
        }
    }
}

/// <summary>Conteúdo gravado na fila (JSON).</summary>
public static class BotQueuePayload
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static BotQueueItem ForMessage(Guid branchId, IncomingMessage message, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        BranchId = branchId,
        Kind = BotQueueKinds.Message,
        ExternalId = message.Id,
        PayloadJson = JsonSerializer.Serialize(message, Json),
        ReceivedAt = nowUtc,
        NextAttemptAt = nowUtc,
    };

    public static BotQueueItem ForStatus(Guid branchId, DeliveryStatus status, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        BranchId = branchId,
        Kind = BotQueueKinds.Status,
        ExternalId = $"{status.WamId}:{status.Status}",
        PayloadJson = JsonSerializer.Serialize(status, Json),
        ReceivedAt = nowUtc,
        NextAttemptAt = nowUtc,
    };

    public static IncomingMessage Message(BotQueueItem item) =>
        JsonSerializer.Deserialize<IncomingMessage>(item.PayloadJson, Json) ?? throw new InvalidOperationException("Item da fila vazio.");

    public static DeliveryStatus Status(BotQueueItem item) =>
        JsonSerializer.Deserialize<DeliveryStatus>(item.PayloadJson, Json) ?? throw new InvalidOperationException("Item da fila vazio.");
}

/// <summary>
/// Processa a fila do webhook gravada no banco, um item por vez e na ordem de chegada (cada um no próprio escopo). O que
/// falha volta com espera crescente (30 s, 1, 2, 4 min) e, na 5ª vez, fica como "failed" (aparece no /health). Item que
/// ficou "processando" porque a API caiu no meio volta sozinho quando o prazo vence. A cada 6 h apaga a conversa e os ids
/// processados mais antigos que a retenção (<c>WhatsApp:RetentionDays</c>).
/// </summary>
public sealed class BotWorker(
    BotSignal signal,
    IBotStore store,
    AgendaChangeFeed feed,
    IServiceScopeFactory scopes,
    IOptions<WhatsAppOptions> options,
    ILogger<BotWorker> logger) : BackgroundService
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan PurgeEvery = TimeSpan.FromHours(6);
    private static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(5);

    /// <summary>Tempo que o worker segura um item; se cair antes de terminar, outro pega depois disso.</summary>
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    /// <summary>A Meta reenvia por até 7 dias: guardar o id por esse tempo basta para não processar duas vezes.</summary>
    private static readonly TimeSpan KeepProcessedIds = TimeSpan.FromDays(7);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(ConsumeAsync(stoppingToken), PurgeLoopAsync(stoppingToken));

    private async Task ConsumeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(ct))
                {
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Banco fora do ar: espera e tenta de novo; nada se perde, está tudo na fila.
                logger.LogError(ex, "Bot: não foi possível ler a fila do webhook.");
            }
            await signal.WaitAsync(IdleCheck, ct);
        }
    }

    /// <summary>Processa um item. Falso quando a fila está vazia (ou nada venceu ainda).</summary>
    private async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var item = await store.ClaimNextAsync(now, now + Lease, ct);
        if (item is null) return false;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<AgendaActor>().Name = AgendaActor.Bot;
            if (item.Kind == BotQueueKinds.Message)
            {
                await sp.GetRequiredService<BookingFlow>()
                    .HandleAsync(item.BranchId, BotQueuePayload.Message(item), sp.GetRequiredService<IMessagingChannel>(), ct);
                // O bot pode ter agendado, remarcado, cancelado ou chamado a equipe: os aparelhos atualizam na hora.
                feed.Publish(item.BranchId, AgendaChangeFeed.Inbox, AgendaChangeFeed.Bookings, AgendaChangeFeed.Reminders, AgendaChangeFeed.Clients);
            }
            else
            {
                var status = BotQueuePayload.Status(item);
                await sp.GetRequiredService<IAgendaService>().MarkDeliveryAsync(item.BranchId,
                    new DeliveryRequest { WamId = status.WamId, Status = status.Status, Erro = status.Error }, ct);
                feed.Publish(item.BranchId, AgendaChangeFeed.Reminders);
            }
            await store.CompleteAsync(item.Id, DateTime.UtcNow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var giveUp = item.Attempts >= MaxAttempts;
            var next = DateTime.UtcNow + TimeSpan.FromSeconds(30 * Math.Pow(2, Math.Max(0, item.Attempts - 1)));
            logger.Log(giveUp ? LogLevel.Error : LogLevel.Warning, ex,
                "Bot: falha no item {Kind} {Id} da filial {BranchId} (tentativa {Attempt} de {Max}){Final}.",
                item.Kind, item.Id, item.BranchId, item.Attempts, MaxAttempts, giveUp ? "; desisti" : "; tento de novo");
            await store.RetryLaterAsync(item.Id, ex.Message, next, giveUp, CancellationToken.None);
        }
        return true;
    }

    private async Task PurgeLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PurgeEvery);
        try
        {
            do
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var days = Math.Max(1, options.Value.RetentionDays);
                    var removed = await store.PurgeAsync(now - KeepProcessedIds, now.AddDays(-days), ct);
                    if (removed > 0) logger.LogInformation("Bot: {Count} registro(s) antigo(s) apagado(s) (retenção de {Days} dias).", removed, days);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Bot: não foi possível apagar a conversa antiga.");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
