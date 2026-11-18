using Application.Abstractions;
using Application.Agenda;

namespace Api.Agenda;

/// <summary>
/// A cada minuto confere a lista de espera das filiais que têm alguém esperando: avisa pelo WhatsApp quem tem vaga agora
/// (só das 8h às 21h), marca quem já marcou horário e expira os dias que passaram (<see cref="WaitlistService"/>).
/// </summary>
public sealed class WaitlistWorker(IServiceScopeFactory scopes, AgendaChangeFeed feed, ILogger<WaitlistWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> branches;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            branches = await scope.ServiceProvider.GetRequiredService<IAgendaRepository>().ListBranchesWithWaitlistAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Lista de espera: não foi possível listar as filiais.");
            return;
        }
        foreach (var branchId in branches)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var changed = await scope.ServiceProvider.GetRequiredService<WaitlistService>().NotifyAsync(branchId, ct);
                if (changed > 0)
                {
                    logger.LogInformation("Lista de espera: {Count} pedido(s) atualizado(s) na filial {BranchId}.", changed, branchId);
                    feed.Publish(branchId, AgendaChangeFeed.Bookings, AgendaChangeFeed.Inbox);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lista de espera: falha na filial {BranchId}.", branchId);
            }
        }
    }
}
