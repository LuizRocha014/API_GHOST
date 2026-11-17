using System.Text.Json;
using Domain.Agenda;

namespace Application.Agenda;

/// <summary>Serviço do plano guardado em <c>ItemsJson</c>.</summary>
public sealed record PlanItemModel(string ServiceId, int TimesPerMonth);

/// <summary>Regras puras dos planos de fidelidade (preço, mês de uso, qual plano cobre um agendamento).</summary>
public static class LoyaltyRules
{
    public const int MaxTimesPerMonth = 31;
    public const int MaxItems = 50;

    public static IReadOnlyList<PlanItemModel> ParseItems(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<PlanItemModel>>(json, AgendaConfigModel.Json) ?? [];

    public static string ItemsJson(IEnumerable<PlanItemModel> items) => JsonSerializer.Serialize(items, AgendaConfigModel.Json);

    /// <summary>Valor com o desconto, em centavos arredondados (meio centavo para cima).</summary>
    public static decimal ApplyDiscount(decimal fullPrice, decimal discountPercent) =>
        Math.Round(fullPrice * (100m - discountPercent) / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Valor cheio do mês para o tipo de veículo: soma de preço × vezes. Nulo se algum serviço não tem preço para o tipo.</summary>
    public static decimal? FullPrice(AgendaConfigModel cfg, IEnumerable<PlanItemModel> items, string vehicleType)
    {
        var total = 0m;
        foreach (var item in items)
        {
            var price = cfg.ServiceById(item.ServiceId)?.PriceFor(vehicleType);
            if (price is null) return null;
            total += price.Value * item.TimesPerMonth;
        }
        return total;
    }

    /// <summary>Primeiro e último dia do mês da data.</summary>
    public static (DateTime From, DateTime To) Month(DateTime date)
    {
        var from = new DateTime(date.Year, date.Month, 1);
        return (from, from.AddMonths(1).AddDays(-1));
    }

    public static string MonthKey(DateTime date) => date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>O plano vale no dia: começou e ainda não acabou (o dia do encerramento ainda conta).</summary>
    public static bool Covers(AgendaClientPlan plan, DateTime date) =>
        plan.Active && date.Date >= plan.StartDate.Date && (plan.EndDate is null || date.Date <= plan.EndDate.Value.Date);

    /// <summary>
    /// Meses com mensalidade: do mês da adesão até o mês do encerramento (ou o mês de <paramref name="today"/>).
    /// O mês da adesão e o do encerramento contam inteiros.
    /// </summary>
    public static IReadOnlyList<DateTime> ChargeMonths(AgendaClientPlan plan, DateTime today)
    {
        var first = Month(plan.StartDate).From;
        var last = Month(plan.EndDate ?? today).From;
        var months = new List<DateTime>();
        for (var m = first; m <= last; m = m.AddMonths(1)) months.Add(m);
        return months;
    }

    /// <summary>Mensalidades do plano, da mais recente para a mais antiga: pagas com o valor pago, as outras pendentes com o valor atual.</summary>
    public static IReadOnlyList<PlanChargeDto> Charges(AgendaClientPlan plan, IEnumerable<AgendaPlanPayment> payments, DateTime today)
    {
        var paid = payments.Where(p => p.ClientPlanId == plan.Id && p.Active).ToDictionary(p => p.Month.Date);
        return ChargeMonths(plan, today)
            .OrderByDescending(m => m)
            .Select(m => paid.TryGetValue(m, out var p)
                ? new PlanChargeDto(MonthKey(m), p.Amount, "paid", AgendaFormat.LocalDateTime(p.PaidAt))
                : new PlanChargeDto(MonthKey(m), plan.Price, "pending", null))
            .ToList();
    }

    /// <summary>'AAAA-MM' → primeiro dia do mês.</summary>
    public static DateTime ParseMonth(string? key) =>
        DateTime.TryParseExact(key, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d
            : throw new AgendaValidationException("Mês inválido (use AAAA-MM).");

    /// <summary>
    /// Plano do cliente que vale para o carro do agendamento: o do próprio carro, ou o plano sem carro do mesmo tipo de veículo.
    /// </summary>
    public static AgendaClientPlan? PlanFor(IEnumerable<AgendaClientPlan> plans, Guid? vehicleId, string vehicleType, DateTime date)
    {
        var valid = plans.Where(p => Covers(p, date)).ToList();
        return (vehicleId is null ? null : valid.FirstOrDefault(p => p.VehicleId == vehicleId))
               ?? valid.FirstOrDefault(p => p.VehicleId is null && p.VehicleType == vehicleType);
    }
}
