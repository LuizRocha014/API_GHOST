using Application.Abstractions;
using Domain.Agenda;

namespace Application.Agenda;

/// <summary>Aviso de mensalidade processado: para quem, de qual mês, e se saiu.</summary>
public sealed record PlanNoticeDto(
    string ClientPlanId, string ClientName, string Phone, string Type, string Month, string DueDate, decimal Amount, string Status, string? Error);

/// <summary>
/// Avisos de mensalidade pelo WhatsApp (só para clientes que aceitaram mensagens): "vence em X dias" e "em atraso", um de cada
/// por plano e mês. Olha o mês atual e o anterior (não manda cobrança de meses antigos de uma vez).
/// </summary>
public sealed class PlanBillingService(IAgendaRepository repo, IAgendaService agenda, IAgendaClock clock, IPlanNoticeSender sender)
{
    public async Task<IReadOnlyList<PlanNoticeDto>> SendNoticesAsync(Guid branchId, CancellationToken ct = default)
    {
        var cfg = AgendaConfigModel.Parse(await agenda.GetConfigAsync(branchId, ct));
        var billing = cfg.PlanBilling;
        if (!billing.NotifyDue && !billing.NotifyOverdue) return [];

        var today = clock.NowLocal.Date;
        var plans = (await repo.ListBranchClientPlansAsync(branchId, null, ct)).Where(p => p.Active && p.EndDate is null).ToList();
        if (plans.Count == 0) return [];
        var payments = await repo.ListPlanPaymentsAsync(branchId, null, ct);
        var clients = (await repo.ListClientsAsync(branchId, ct)).ToDictionary(c => c.Id);
        var firstMonth = LoyaltyRules.Month(today).From.AddMonths(-1);

        var result = new List<PlanNoticeDto>();
        foreach (var plan in plans)
        {
            if (!clients.TryGetValue(plan.ClientId, out var client) || client.Phone.Length == 0 || !client.ReminderOptIn) continue;
            foreach (var charge in LoyaltyRules.Charges(plan, payments, today, billing.GraceDays).Where(c => c.Status == "pending"))
            {
                var month = LoyaltyRules.ParseMonth(charge.Month);
                if (month < firstMonth) continue;
                var due = LoyaltyRules.DueDate(plan, month);
                string? type = null;
                if (charge.Overdue && billing.NotifyOverdue) type = PlanNoticeTypes.Overdue;
                else if (!charge.Overdue && billing.NotifyDue && today >= due.AddDays(-Math.Max(0, billing.NotifyDaysBefore)) && today <= due)
                    type = PlanNoticeTypes.DueSoon;
                if (type is null) continue;

                var notice = new AgendaPlanNotice
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    ClientPlanId = plan.Id,
                    Month = month,
                    Type = type,
                    Status = "sending",
                    CreatedAt = DateTime.UtcNow,
                };
                // Reserva antes de mandar: duas instâncias (ou duas rodadas) não avisam duas vezes.
                if (!await repo.TryInsertPlanNoticeAsync(notice, ct)) continue;
                try
                {
                    var values = BotTemplatesValues(cfg, client.Name, plan.Name, charge.Amount, due);
                    notice.WamId = await sender.SendAsync(type, branchId, client.Phone, values, cfg, ct);
                    notice.Status = "sent";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    notice.Status = "failed";
                    notice.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                }
                await repo.UpdatePlanNoticeAsync(notice, ct);
                result.Add(new PlanNoticeDto(plan.Id.ToString(), client.Name, client.Phone, type, charge.Month, AgendaFormat.Date(due), charge.Amount,
                    notice.Status, notice.Error));
            }
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> BotTemplatesValues(
        AgendaConfigModel cfg, string clientName, string planName, decimal amount, DateTime due) =>
        Bot.BotTemplates.PlanValues(cfg, clientName, planName, amount, due);
}
