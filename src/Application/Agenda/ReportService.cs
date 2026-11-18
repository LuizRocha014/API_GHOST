using System.Globalization;
using System.Text;
using Application.Abstractions;
using Domain.Agenda;

namespace Application.Agenda;

/// <summary>Dinheiro do período. <c>Received</c>: recebido nos atendimentos; <c>ToReceive</c>: concluídos sem pagamento.</summary>
public sealed record ReportMoneyDto(decimal Received, decimal ToReceive, decimal Discounts, decimal PlanCharges, decimal AverageTicket, int PaidCount);

/// <summary>Contagem do período. Remarcados não contam como cancelados (o agendamento novo é que conta).</summary>
public sealed record ReportCountsDto(
    int Total, int Done, int Scheduled, int InProgress, int Canceled, int NoShow, int Rescheduled, decimal NoShowRate, int CoveredByPlan,
    int ByWhatsApp, int AtCounter);

/// <summary>Linha de um agrupamento: <c>Amount</c> = valor dos concluídos; <c>Share</c> = % (ocupação no box, fatia no resto).</summary>
public sealed record ReportLineDto(string Key, string Name, int Count, decimal Amount, decimal? Share = null);

public sealed record ReportDayDto(string Date, int Done, decimal Received);

public sealed record ReportPlansDto(int Active, decimal MonthlyRecurring, int PendingCharges);

/// <summary>Avaliações dos clientes no período (botões Ótimo = 5, Bom = 3, Ruim = 1). <c>Average</c> nulo = nenhuma avaliação.</summary>
public sealed record ReportRatingsDto(int Count, decimal? Average, int Great, int Good, int Bad);

public sealed record ReportDto(
    string From,
    string To,
    ReportMoneyDto Money,
    ReportCountsDto Counts,
    IReadOnlyList<ReportLineDto> ByService,
    IReadOnlyList<ReportLineDto> ByBox,
    IReadOnlyList<ReportLineDto> ByMethod,
    IReadOnlyList<ReportDayDto> ByDay,
    IReadOnlyList<ReportLineDto> TopClients,
    ReportPlansDto Plans,
    ReportRatingsDto? Ratings = null);

/// <summary>
/// Relatórios do dono: faturamento por período, serviço, box e forma de pagamento; ticket médio; faltas; planos ativos e
/// receita recorrente; e a planilha dos atendimentos (texto separado por tabulação, para colar no Excel ou no Google Planilhas).
/// </summary>
public sealed class ReportService(IAgendaRepository repo, IAgendaService agenda, IAgendaClock clock)
{
    public const int MaxDays = 400;

    private static (DateTime From, DateTime To) Range(string from, string to)
    {
        var f = AgendaFormat.ParseDate(from);
        var t = AgendaFormat.ParseDate(to);
        if (t < f) throw new AgendaValidationException("O fim do período vem antes do começo.");
        if ((t - f).TotalDays > MaxDays) throw new AgendaValidationException($"Escolha um período de até {MaxDays} dias.");
        return (f, t);
    }

    /// <summary>Valor do atendimento concluído: o recebido, ou o preço menos o desconto (ainda a receber).</summary>
    private static decimal Value(AgendaBooking b) => b.PaidAmount ?? Math.Max(0, (b.Price ?? 0) - (b.Discount ?? 0));

    public async Task<ReportDto> BuildAsync(Guid branchId, string from, string to, CancellationToken ct = default)
    {
        var (f, t) = Range(from, to);
        var cfg = AgendaConfigModel.Parse(await agenda.GetConfigAsync(branchId, ct));
        var all = await repo.ListBookingsAsync(branchId, f, t, ct);
        var rescheduledIds = all.Where(b => b.RescheduledFrom is not null).Select(b => b.RescheduledFrom!.Value).ToHashSet();
        var bookings = all.Where(b => !rescheduledIds.Contains(b.Id) || b.Status != BookingStatuses.Canceled).ToList();
        var done = bookings.Where(b => b.Status == BookingStatuses.Done).ToList();
        var paid = bookings.Where(b => b.PaidAt is not null && b.Status != BookingStatuses.Canceled).ToList();

        // Mensalidades pagas no período e planos ativos.
        var plans = await repo.ListBranchClientPlansAsync(branchId, null, ct);
        var payments = await repo.ListPlanPaymentsAsync(branchId, null, ct);
        var planCharges = payments.Where(p => p.PaidAt.Date >= f && p.PaidAt.Date <= t).Sum(p => p.Amount);
        var today = clock.NowLocal.Date;
        var active = plans.Where(p => p.StartDate.Date <= today && (p.EndDate is null || p.EndDate.Value.Date >= today)).ToList();
        var byPlan = payments.ToLookup(p => p.ClientPlanId);
        var pendingCharges = active.Sum(p => LoyaltyRules.Charges(p, byPlan[p.Id], today).Count(c => c.Status == "pending"));

        var received = paid.Sum(b => b.PaidAmount ?? 0);
        var money = new ReportMoneyDto(
            received,
            done.Where(b => b.PaidAt is null).Sum(Value),
            paid.Sum(b => b.Discount ?? 0),
            planCharges,
            paid.Count == 0 ? 0 : Math.Round(received / paid.Count, 2),
            paid.Count);

        var noShow = bookings.Count(b => b.Status == BookingStatuses.NoShow);
        var counts = new ReportCountsDto(
            bookings.Count,
            done.Count,
            bookings.Count(b => b.Status == BookingStatuses.Scheduled),
            bookings.Count(b => b.Status == BookingStatuses.InProgress),
            bookings.Count(b => b.Status == BookingStatuses.Canceled),
            noShow,
            rescheduledIds.Count(id => all.Any(b => b.Id == id)),
            done.Count + noShow == 0 ? 0 : Math.Round(100m * noShow / (done.Count + noShow), 1),
            bookings.Count(b => b.ClientPlanId is not null),
            bookings.Count(b => b.Origin == BookingOrigins.WhatsApp),
            bookings.Count(b => b.Origin != BookingOrigins.WhatsApp));

        var doneValue = done.Sum(Value);
        var byService = done.GroupBy(b => b.ServiceId)
            .Select(g => new ReportLineDto(g.Key, g.Last().ServiceName, g.Count(), g.Sum(Value), Share(g.Sum(Value), doneValue)))
            .OrderByDescending(x => x.Amount).ThenByDescending(x => x.Count).ToList();

        var byMethod = paid.GroupBy(b => b.PaymentMethod ?? PaymentMethods.Other)
            .Select(g => new ReportLineDto(g.Key, PaymentMethods.Label(g.Key), g.Count(), g.Sum(b => b.PaidAmount ?? 0), Share(g.Sum(b => b.PaidAmount ?? 0), received)))
            .OrderByDescending(x => x.Amount).ToList();

        // Ocupação: minutos marcados (concluídos, em atendimento e agendados) sobre os minutos abertos de cada box.
        var occupying = bookings.Where(b => b.Status is BookingStatuses.Done or BookingStatuses.InProgress or BookingStatuses.Scheduled).ToList();
        var byBox = cfg.Boxes.Select(box =>
        {
            var open = 0;
            for (var d = f; d <= t; d = d.AddDays(1))
            {
                if (AvailabilityRules.HoursOf(cfg, d) is not { } h) continue;
                var blocked = AvailabilityRules.RecurringBlocks(cfg, d)
                    .Where(k => k.BoxId is null || k.BoxId == box.Id)
                    .Sum(k => Math.Max(0, Math.Min(k.End, h.End) - Math.Max(k.Start, h.Start)));
                open += Math.Max(0, h.Length - blocked);
            }
            var mine = occupying.Where(b => b.BoxId == box.Id).ToList();
            var busy = mine.Sum(b => b.EndMin - b.StartMin);
            var doneHere = done.Where(b => b.BoxId == box.Id).ToList();
            return new ReportLineDto(box.Id, box.Name, doneHere.Count, doneHere.Sum(Value), open == 0 ? 0 : Math.Round(100m * busy / open, 1));
        }).ToList();

        var byDay = new List<ReportDayDto>();
        for (var d = f; d <= t; d = d.AddDays(1))
        {
            var day = d;
            byDay.Add(new ReportDayDto(
                AgendaFormat.Date(day),
                done.Count(b => b.Date.Date == day),
                paid.Where(b => b.Date.Date == day).Sum(b => b.PaidAmount ?? 0)));
        }

        var topClients = done
            .GroupBy(b => b.ClientId?.ToString() ?? (b.Phone.Length > 0 ? b.Phone : b.ClientName.Trim().ToLowerInvariant()))
            .Select(g => new ReportLineDto(g.Key, g.Last().ClientName, g.Count(), g.Sum(Value), Share(g.Sum(Value), doneValue)))
            .OrderByDescending(x => x.Amount).ThenByDescending(x => x.Count).Take(10).ToList();

        var rated = done.Where(b => b.Rating is not null).Select(b => b.Rating!.Value).ToList();
        var ratings = new ReportRatingsDto(
            rated.Count,
            rated.Count == 0 ? null : Math.Round((decimal)rated.Average(), 1),
            rated.Count(r => r >= 5),
            rated.Count(r => r is >= 3 and < 5),
            rated.Count(r => r < 3));

        return new ReportDto(
            AgendaFormat.Date(f), AgendaFormat.Date(t), money, counts, byService, byBox, byMethod, byDay, topClients,
            new ReportPlansDto(active.Count, active.Sum(p => p.Price), pendingCharges), ratings);
    }

    private static decimal Share(decimal part, decimal total) => total == 0 ? 0 : Math.Round(100m * part / total, 1);

    /// <summary>Atendimentos do período, um por linha, separados por tabulação (cola direto numa planilha).</summary>
    public async Task<string> SpreadsheetAsync(Guid branchId, string from, string to, CancellationToken ct = default)
    {
        var (f, t) = Range(from, to);
        var cfg = AgendaConfigModel.Parse(await agenda.GetConfigAsync(branchId, ct));
        var rows = (await repo.ListBookingsAsync(branchId, f, t, ct)).OrderBy(b => b.Date).ThenBy(b => b.StartMin).ToList();
        var pt = new CultureInfo("pt-BR");
        string Money(decimal? v) => v is null ? string.Empty : v.Value.ToString("0.00", pt);
        string Clean(string s) => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        var status = new Dictionary<string, string>
        {
            [BookingStatuses.Scheduled] = "Agendado",
            [BookingStatuses.InProgress] = "Em atendimento",
            [BookingStatuses.Done] = "Concluído",
            [BookingStatuses.Canceled] = "Cancelado",
            [BookingStatuses.NoShow] = "Não compareceu",
        };
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('\t', "Data", "Início", "Fim", "Box", "Cliente", "WhatsApp", "Veículo", "Placa", "Serviço", "Status", "Origem",
            "Plano", "Preço", "Desconto", "Recebido", "Forma", "Recebido em", "Avaliação"));
        foreach (var b in rows)
        {
            sb.AppendLine(string.Join('\t',
                b.Date.ToString("dd/MM/yyyy", pt),
                AgendaFormat.Hm(b.StartMin),
                AgendaFormat.Hm(b.EndMin),
                Clean(cfg.BoxById(b.BoxId)?.Name ?? b.BoxId),
                Clean(b.ClientName),
                b.Phone,
                Clean(b.Vehicle),
                b.Plate,
                Clean(BookingExtras.Title(b.ServiceName, BookingExtras.Parse(b.ExtrasJson))),
                status.GetValueOrDefault(b.Status, b.Status),
                b.Origin == BookingOrigins.WhatsApp ? "WhatsApp" : "Balcão",
                b.ClientPlanId is null ? string.Empty : "Sim",
                Money(b.Price),
                Money(b.Discount),
                Money(b.PaidAmount),
                b.PaymentMethod is null ? string.Empty : PaymentMethods.Label(b.PaymentMethod),
                b.PaidAt is { } at ? clock.ToLocal(at).ToString("dd/MM/yyyy HH:mm", pt) : string.Empty,
                b.Rating is { } r ? AgendaService.RatingLabel(r) : string.Empty));
        }
        return sb.ToString();
    }
}
