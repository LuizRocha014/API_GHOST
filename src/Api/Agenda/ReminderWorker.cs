using Application.Agenda;

namespace Api.Agenda;

/// <summary>
/// Envia os lembretes D-1 vencidos de todas as filiais a cada <c>Agenda:ReminderWorker:IntervalSeconds</c>
/// (padrão 60 s). Liga e desliga por <c>Agenda:ReminderWorker:Enabled</c>. Sem WhatsApp configurado, o envio
/// só fica registrado no log (ver <c>LocalReminderSender</c>).
/// </summary>
public sealed class ReminderWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ReminderWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Max(10, configuration.GetValue("Agenda:ReminderWorker:IntervalSeconds", 60));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> branches;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            branches = await scope.ServiceProvider.GetRequiredService<IAgendaService>().BranchesWithDueRemindersAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Lembretes: não foi possível consultar o banco.");
            return;
        }

        foreach (var branchId in branches)
        {
            try
            {
                // Um escopo por filial: cada uma tem a própria conexão e transação.
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IAgendaService>()
                    .DispatchRemindersAsync(branchId, new DispatchRequest(), ct);
                if (result.Count > 0)
                    logger.LogInformation("Lembretes: {Count} processado(s) na filial {BranchId}.", result.Count, branchId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lembretes: falha ao processar a filial {BranchId}.", branchId);
            }
        }
    }
}
