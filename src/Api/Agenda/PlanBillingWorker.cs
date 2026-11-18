using Application.Abstractions;
using Application.Agenda;

namespace Api.Agenda;

/// <summary>
/// De hora em hora, das 9h às 19h no fuso da estética, manda os avisos de mensalidade (vencendo e em atraso) das filiais
/// que ligaram isso em Ajustes. Cada aviso sai uma vez por plano e mês (<see cref="PlanBillingService"/>).
/// </summary>
public sealed class PlanBillingWorker(
    IServiceScopeFactory scopes, IAgendaClock clock, AgendaChangeFeed feed, ILogger<PlanBillingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            // Primeira rodada um pouco depois de subir (a API ainda está aplicando migrações e aquecendo).
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            do
            {
                if (clock.NowLocal.Hour is >= 9 and < 19) await RunOnceAsync(stoppingToken);
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
            branches = await scope.ServiceProvider.GetRequiredService<IAgendaRepository>().ListBranchIdsAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Mensalidades: não foi possível listar as filiais.");
            return;
        }
        foreach (var branchId in branches)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sent = await scope.ServiceProvider.GetRequiredService<PlanBillingService>().SendNoticesAsync(branchId, ct);
                if (sent.Count > 0)
                {
                    logger.LogInformation("Mensalidades: {Count} aviso(s) na filial {BranchId} ({Failed} com falha).",
                        sent.Count, branchId, sent.Count(n => n.Status == "failed"));
                    feed.Publish(branchId, AgendaChangeFeed.Inbox, AgendaChangeFeed.Plans);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Mensalidades: falha ao avisar na filial {BranchId}.", branchId);
            }
        }
    }
}
