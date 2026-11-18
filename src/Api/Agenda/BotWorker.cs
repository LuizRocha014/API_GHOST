using System.Threading.Channels;
using Application.Agenda;
using Application.Bot;
using Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace Api.Agenda;

/// <summary>Item da fila: uma mensagem do cliente ou um status de entrega, já com a filial.</summary>
public sealed record BotWork(Guid BranchId, IncomingMessage? Message, DeliveryStatus? Status);

/// <summary>
/// Fila em memória entre o webhook e o bot: o webhook só valida, enfileira e responde 200 na hora
/// (a Meta reenvia o que demora); o <see cref="BotWorker"/> processa na ordem de chegada.
/// </summary>
public sealed class BotQueue
{
    private readonly Channel<BotWork> _channel = Channel.CreateBounded<BotWork>(new BoundedChannelOptions(10_000)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    /// <summary>Falso quando a fila está cheia: o webhook devolve 503 e a Meta tenta de novo depois.</summary>
    public bool TryEnqueue(BotWork work) => _channel.Writer.TryWrite(work);

    public IAsyncEnumerable<BotWork> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Processa a fila do webhook (um item por vez, cada um no próprio escopo) e, a cada 6 h, apaga a conversa
/// e os ids processados mais antigos que a retenção (<c>WhatsApp:RetentionDays</c>).
/// </summary>
public sealed class BotWorker(
    BotQueue queue,
    IServiceScopeFactory scopes,
    IOptions<WhatsAppOptions> options,
    ILogger<BotWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PurgeEvery = TimeSpan.FromHours(6);

    /// <summary>A Meta reenvia por até 7 dias: guardar o id por esse tempo basta para não processar duas vezes.</summary>
    private static readonly TimeSpan KeepProcessedIds = TimeSpan.FromDays(7);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(ConsumeAsync(stoppingToken), PurgeLoopAsync(stoppingToken));

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var work in queue.ReadAllAsync(ct))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var sp = scope.ServiceProvider;
                    if (work.Message is not null)
                        await sp.GetRequiredService<BookingFlow>().HandleAsync(work.BranchId, work.Message, sp.GetRequiredService<IMessagingChannel>(), ct);
                    else if (work.Status is { } status)
                        await sp.GetRequiredService<IAgendaService>().MarkDeliveryAsync(work.BranchId,
                            new DeliveryRequest { WamId = status.WamId, Status = status.Status, Erro = status.Error }, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Bot: falha ao processar item da fila da filial {BranchId}.", work.BranchId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
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
                    await using var scope = scopes.CreateAsyncScope();
                    var now = DateTime.UtcNow;
                    var days = Math.Max(1, options.Value.RetentionDays);
                    var removed = await scope.ServiceProvider.GetRequiredService<IBotStore>()
                        .PurgeAsync(now - KeepProcessedIds, now.AddDays(-days), ct);
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
