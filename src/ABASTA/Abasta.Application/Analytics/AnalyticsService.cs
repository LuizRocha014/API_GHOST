using System.Globalization;
using Abasta.Application.Abstractions;

namespace Abasta.Application.Analytics;

public interface IAnalyticsService
{
    Task<TeamOverviewDto> GetTeamOverviewAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<ReportDto> GetReportAsync(Guid companyId, Guid? userId, CancellationToken cancellationToken = default);
    Task<DashboardDto> GetDashboardAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private static readonly string[] MonthAbbr =
        { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };
    private static readonly string[] MonthFull =
    {
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro",
    };

    private readonly IAnalyticsRepository _repo;

    public AnalyticsService(IAnalyticsRepository repo) => _repo = repo;

    // ───────────────────────── Equipe ─────────────────────────
    public async Task<TeamOverviewDto> GetTeamOverviewAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var users = await _repo.GetUsersAsync(companyId, cancellationToken).ConfigureAwait(false);
        var entries = await _repo.GetEntriesAsync(companyId, null, cancellationToken).ConfigureAwait(false);
        var plates = await _repo.GetUserVehiclePlatesAsync(companyId, cancellationToken).ConfigureAwait(false);
        var account = await _repo.GetAccountNameAsync(companyId, cancellationToken).ConfigureAwait(false);

        var month = entries.Where(e => SameMonth(e.FueledAt, now)).ToList();
        var byUser = month.GroupBy(e => e.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var collaborators = users.Where(u => u.Role == "collaborator").ToList();
        var members = collaborators.Select(u =>
        {
            var es = byUser.TryGetValue(u.Id, out var list) ? list : new();
            return new TeamMemberDto(
                u.Id.ToString(), u.FullName, u.JobTitle ?? "Colaborador",
                plates.TryGetValue(u.Id, out var p) ? p : string.Empty, u.Email,
                es.Sum(e => e.TotalAmount), es.Sum(e => e.Liters), es.Count, 0);
        }).ToList();

        var fuelBreakdown = month
            .GroupBy(e => e.FuelCode)
            .Select(g => new FuelBreakdownDto(g.Key, g.Sum(e => e.TotalAmount)))
            .OrderByDescending(f => f.Value)
            .ToList();

        return new TeamOverviewDto(
            MonthLabel: $"Equipe · {MonthAbbr[now.Month - 1]}",
            AccountName: account ?? "sua distribuidora",
            ActiveCount: collaborators.Count,
            TotalSpent: month.Sum(e => e.TotalAmount),
            TotalLiters: month.Sum(e => e.Liters),
            TotalEntries: month.Count,
            DeltaReaisVsLastMonth: 0,
            Members: members,
            Alerts: BuildAlerts(members, month),
            FuelBreakdown: fuelBreakdown);
    }

    private static IReadOnlyList<AlertDto> BuildAlerts(List<TeamMemberDto> members, List<AnalyticsEntry> month)
    {
        var alerts = new List<AlertDto>();
        if (members.Count == 0) return alerts;

        var top = members.OrderByDescending(m => m.Total).First();
        if (top.Total > 0)
            alerts.Add(new AlertDto("warn", $"{top.Name} é quem mais gastou", "Veja o detalhe do colaborador."));

        var missingKm = month.Count(e => e.OdometerKm is null or 0);
        if (missingKm > 0)
            alerts.Add(new AlertDto("danger", $"{missingKm} notas sem KM",
                "Peça para registrarem o KM do painel."));

        alerts.Add(new AlertDto("success", "Equipe ativa", $"{members.Count} colaboradores no painel."));
        return alerts;
    }

    // ───────────────────────── Relatórios ─────────────────────────
    public async Task<ReportDto> GetReportAsync(Guid companyId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entries = await _repo.GetEntriesAsync(companyId, userId, cancellationToken).ConfigureAwait(false);

        var months = Enumerable.Range(0, 6).Select(i => new DateTime(now.Year, now.Month, 1).AddMonths(-5 + i)).ToList();
        var monthly = months.Select(m =>
        {
            var sum = entries.Where(e => SameMonth(e.FueledAt, m)).Sum(e => e.TotalAmount);
            return new MonthPointDto(MonthAbbr[m.Month - 1], sum);
        }).ToList();
        var total6 = monthly.Sum(m => m.Value);
        var lastMonth = monthly.Count > 0 ? monthly[^1].Value : 0;

        var fuelTotal = entries.Sum(e => e.TotalAmount);
        var fuelSlices = entries
            .GroupBy(e => e.FuelCode)
            .Select(g =>
            {
                var v = g.Sum(e => e.TotalAmount);
                return new FuelSliceDto(g.Key, v, fuelTotal == 0 ? 0 : (int)Math.Round(v / fuelTotal * 100));
            })
            .OrderByDescending(s => s.Value)
            .ToList();

        var topStations = entries
            .GroupBy(e => e.StationName ?? "Posto")
            .Select(g => new StationStatDto(g.Key, g.Count(), g.Sum(e => e.TotalAmount)))
            .OrderByDescending(s => s.Value)
            .Take(4)
            .ToList();

        return new ReportDto(
            IsAdmin: userId is null,
            LastMonthValue: lastMonth,
            Total6Months: total6,
            MonthlyAvg: total6 / 6,
            ScopeCount: userId is null ? entries.Select(e => e.UserId).Distinct().Count() : 1,
            Monthly: monthly,
            FuelSlices: fuelSlices,
            TopStations: topStations);
    }

    // ───────────────────────── Dashboard (colaborador) ─────────────────────────
    public async Task<DashboardDto> GetDashboardAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entries = await _repo.GetEntriesAsync(companyId, userId, cancellationToken).ConfigureAwait(false);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var budget = await _repo.GetBudgetAsync(companyId, userId, monthStart, cancellationToken).ConfigureAwait(false);

        var month = entries.Where(e => SameMonth(e.FueledAt, now)).ToList();
        var recent = entries.Take(3).Select(e => new EntryDto(
            e.Id.ToString(), e.UserId.ToString(), string.Empty, e.StationName ?? "",
            e.FuelCode, e.Liters, e.PricePerLiter, e.TotalAmount, e.OdometerKm ?? 0,
            Relative(e.FueledAt, now), DateLabel(e.FueledAt))).ToList();

        var lastDay = DateTime.DaysInMonth(now.Year, now.Month);

        return new DashboardDto(
            MonthLabel: $"{MonthFull[now.Month - 1]} · {now.Year}",
            Spent: month.Sum(e => e.TotalAmount),
            Budget: budget ?? 3000m,
            DeltaReaisVsLastMonth: 0,
            Liters: month.Sum(e => e.Liters),
            AvgConsumption: 0,
            EntriesCount: month.Count,
            DaysToMonthEnd: Math.Max(0, lastDay - now.Day),
            RecentEntries: recent);
    }

    // ───────────────────────── Helpers ─────────────────────────
    private static bool SameMonth(DateTime a, DateTime b) => a.Year == b.Year && a.Month == b.Month;

    private static string DateLabel(DateTime dtUtc)
    {
        var dt = dtUtc.ToLocalTime();
        return $"{dt.Day} {MonthAbbr[dt.Month - 1]} · {dt:HH:mm}";
    }

    private static string Relative(DateTime dtUtc, DateTime nowUtc)
    {
        var diff = nowUtc - dtUtc;
        if (diff.TotalMinutes < 60) return "agora há pouco";
        if (diff.TotalHours < 24) return $"há {(int)diff.TotalHours} {((int)diff.TotalHours == 1 ? "hora" : "horas")}";
        var dt = dtUtc.ToLocalTime();
        return $"{dt.Day} {MonthAbbr[dt.Month - 1]}";
    }
}
