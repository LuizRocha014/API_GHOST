using System.Globalization;
using Domain.Agenda;

namespace Application.Agenda;

/// <summary>Intervalo em minutos do dia.</summary>
public readonly record struct MinuteRange(int Start, int End)
{
    public int Length => End - Start;
}

/// <summary>Agendamento ou bloqueio que ocupa um box num dia.</summary>
public sealed record BusyItem(Guid? BookingId, string? BoxId, int Start, int End);

/// <summary>
/// Cálculo de disponibilidade: funções puras (entra dado, sai lista), as mesmas regras do app.
/// A disponibilidade vem de horário de funcionamento − bloqueios − agendamentos ativos.
/// </summary>
public static class AvailabilityRules
{
    public static int WeekdayIndex(DateTime date) => (int)date.DayOfWeek; // 0 = domingo

    /// <summary>Horário de funcionamento do dia; nulo se fecha (dia sem horário ou feriado em que a estética fecha).</summary>
    public static MinuteRange? HoursOf(AgendaConfigModel cfg, DateTime date)
    {
        var i = WeekdayIndex(date);
        if (i >= cfg.Hours.Count) return null;
        var h = cfg.Hours[i];
        if (h is not { Length: 2 } || h[1] <= h[0]) return null;
        return HolidayRules.ClosedReason(cfg, date) is null ? new MinuteRange(h[0], h[1]) : null;
    }

    /// <summary>Bloqueios semanais que caem no dia (almoço, manutenção fixa).</summary>
    public static IReadOnlyList<BusyItem> RecurringBlocks(AgendaConfigModel cfg, DateTime date) =>
        cfg.RecurringBlocks.Where(r => r.AppliesTo(date))
            .Select(r => new BusyItem(null, string.IsNullOrEmpty(r.BoxId) ? null : r.BoxId, r.Start, r.End))
            .ToList();

    public static int CeilTo(int m, int step) => (m + step - 1) / step * step;

    /// <summary>
    /// Intervalos ocupados de um box (agendamentos ativos + bloqueios), ordenados e mesclados. <paramref name="buffer"/>:
    /// folga em volta de cada agendamento (limpeza do box); bloqueios não ganham folga.
    /// </summary>
    public static List<MinuteRange> BusyIntervals(
        string boxId, IEnumerable<BusyItem> bookings, IEnumerable<BusyItem> blocks, Guid? ignoreBookingId, int buffer = 0)
    {
        var raw = bookings
            .Where(b => b.BoxId == boxId && b.BookingId != ignoreBookingId)
            .Select(b => new MinuteRange(b.Start - buffer, b.End + buffer))
            .Concat(blocks.Where(k => k.BoxId is null || k.BoxId == boxId).Select(x => new MinuteRange(x.Start, x.End)))
            .OrderBy(x => x.Start)
            .ToList();
        var merged = new List<MinuteRange>();
        foreach (var iv in raw)
        {
            if (merged.Count > 0 && iv.Start <= merged[^1].End)
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, iv.End) };
            else
                merged.Add(iv);
        }
        return merged;
    }

    public static List<MinuteRange> FreeWindows(MinuteRange hours, IReadOnlyList<MinuteRange> busy, int from)
    {
        var cur = Math.Max(hours.Start, from);
        var output = new List<MinuteRange>();
        foreach (var iv in busy)
        {
            if (iv.End <= cur) continue;
            if (iv.Start >= hours.End) break;
            if (iv.Start > cur) output.Add(new MinuteRange(cur, iv.Start));
            cur = Math.Max(cur, iv.End);
            if (cur >= hours.End) break;
        }
        if (cur < hours.End) output.Add(new MinuteRange(cur, hours.End));
        return output;
    }

    /// <summary>
    /// Horários de início livres para <paramref name="duration"/> minutos. Cada horário traz os boxes
    /// possíveis, do que deixa menos buraco na agenda para o que deixa mais. O box precisa atender o serviço e os
    /// serviços somados (<paramref name="extraServiceIds"/>). Feriados, bloqueios semanais e a folga entre
    /// atendimentos saem da configuração.
    /// </summary>
    public static List<SlotOptionDto> AvailableStarts(
        AgendaConfigModel cfg,
        DateTime date,
        string serviceId,
        int duration,
        IReadOnlyList<BusyItem> bookings,
        IReadOnlyList<BusyItem> blocks,
        DateTime nowLocal,
        int minNoticeMin,
        Guid? ignoreBookingId,
        IReadOnlyList<string>? extraServiceIds = null)
    {
        var hours = HoursOf(cfg, date);
        if (hours is null || duration <= 0) return [];
        var today = nowLocal.Date;
        if (date.Date < today) return [];
        var step = cfg.Step > 0 ? cfg.Step : 30;
        var from = hours.Value.Start;
        if (date.Date == today)
            from = Math.Max(from, CeilTo((int)nowLocal.TimeOfDay.TotalMinutes + Math.Max(0, minNoticeMin), step));
        var allBlocks = blocks.Concat(RecurringBlocks(cfg, date)).ToList();
        var buffer = Math.Max(0, cfg.BufferMin);

        var byStart = new SortedDictionary<int, List<(string Id, int Slack, int Order)>>();
        for (var order = 0; order < cfg.Boxes.Count; order++)
        {
            var box = cfg.Boxes[order];
            if (!box.Serves(serviceId) || (extraServiceIds is not null && !extraServiceIds.All(box.Serves))) continue;
            var busy = BusyIntervals(box.Id, bookings, allBlocks, ignoreBookingId, buffer);
            foreach (var g in FreeWindows(hours.Value, busy, from))
            {
                for (var t = CeilTo(g.Start, step); t + duration <= g.End; t += step)
                {
                    if (!byStart.TryGetValue(t, out var list)) byStart[t] = list = [];
                    list.Add((box.Id, g.End - (t + duration) + (t - g.Start), order));
                }
            }
        }

        return byStart
            .Select(kv => new SlotOptionDto(
                kv.Key,
                kv.Key + duration,
                kv.Value.OrderBy(x => x.Slack).ThenBy(x => x.Order).Select(x => x.Id).ToList()))
            .ToList();
    }

    /// <summary>
    /// O atendimento de <paramref name="duration"/> minutos cabe começando em <paramref name="start"/> no box, sem olhar o
    /// passo da agenda nem a hora atual (edição de um agendamento que já está marcado ou em andamento).
    /// </summary>
    public static bool Fits(
        AgendaConfigModel cfg, DateTime date, string boxId, int start, int duration,
        IReadOnlyList<BusyItem> bookings, IReadOnlyList<BusyItem> blocks, Guid? ignoreBookingId)
    {
        var hours = HoursOf(cfg, date);
        if (hours is null || duration <= 0 || start < hours.Value.Start || start + duration > hours.Value.End) return false;
        var busy = BusyIntervals(boxId, bookings, blocks.Concat(RecurringBlocks(cfg, date)), ignoreBookingId, Math.Max(0, cfg.BufferMin));
        return busy.All(iv => iv.End <= start || iv.Start >= start + duration);
    }
}

/// <summary>Adicionais e serviços somados ao agendamento (cera, aromatização, um segundo serviço).</summary>
public static class BookingExtras
{
    public const int MaxItems = 10;
    public const string KindExtra = "extra";
    public const string KindService = "service";

    public static IReadOnlyList<BookingExtraDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<BookingExtraDto>>(json, AgendaConfigModel.Json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public static string Json(IEnumerable<BookingExtraDto> items) => System.Text.Json.JsonSerializer.Serialize(items, AgendaConfigModel.Json);

    public static int Duration(IEnumerable<BookingExtraDto> items) => items.Sum(x => Math.Max(0, x.Duration));

    public static decimal Price(IEnumerable<BookingExtraDto> items) => items.Sum(x => x.Price ?? 0m);

    /// <summary>Preço do serviço + adicionais. Nulo só se nada tem preço.</summary>
    public static decimal? Total(decimal? main, IReadOnlyList<BookingExtraDto> items) =>
        main is null && items.All(x => x.Price is null) ? null : (main ?? 0m) + Price(items);

    public static IReadOnlyList<string> ServiceIds(IEnumerable<BookingExtraDto> items) =>
        items.Where(x => x.Kind == KindService).Select(x => x.Id).ToList();

    /// <summary>"Lavagem simples + Cera, Aromatização".</summary>
    public static string Title(string serviceName, IEnumerable<BookingExtraDto> items)
    {
        var names = items.Select(x => x.Name).Where(n => n.Length > 0).ToList();
        return names.Count == 0 ? serviceName : $"{serviceName} + {string.Join(", ", names)}";
    }

    /// <summary>
    /// Ids escolhidos → itens com nome, preço e tempo do tipo de veículo. Vale um adicional do catálogo ou outro serviço ativo
    /// (diferente do principal). Repetidos contam uma vez. Um item que já está no agendamento (<paramref name="current"/>) e saiu
    /// do catálogo (apagado ou desligado) continua como foi marcado.
    /// </summary>
    public static List<BookingExtraDto> Resolve(
        AgendaConfigModel cfg, ServiceModel main, string vehicleType, IEnumerable<string>? ids, bool skipMissing = false,
        IReadOnlyList<BookingExtraDto>? current = null)
    {
        var list = new List<BookingExtraDto>();
        foreach (var id in (ids ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            if (cfg.ExtraById(id) is { Active: true } x)
                list.Add(new BookingExtraDto(x.Id, KindExtra, x.Name, x.PriceFor(vehicleType), x.DurationFor(vehicleType)));
            else if (cfg.ServiceById(id) is { Active: true } s && s.Id != main.Id)
                list.Add(new BookingExtraDto(s.Id, KindService, s.Name, s.PriceFor(vehicleType), s.DurationFor(vehicleType)));
            else if (current?.FirstOrDefault(c => c.Id == id) is { } kept && kept.Id != main.Id)
                list.Add(kept);
            else if (!skipMissing)
                throw new AgendaValidationException("Adicional inválido ou inativo.");
        }
        if (list.Count > MaxItems) throw new AgendaValidationException($"No máximo {MaxItems} adicionais por agendamento.");
        return list;
    }
}

/// <summary>Horário do lembrete D-1 (mesmas regras do app).</summary>
public static class ReminderRules
{
    public static DateTime? SendTime(DateTime date, int start, DateTime createdAtLocal, ReminderPolicyModel policy)
    {
        var startsAt = date.Date.AddMinutes(start);
        if (startsAt - createdAtLocal < TimeSpan.FromHours(policy.MinLeadHours)) return null;

        var windowStart = Math.Min(policy.WindowStart, policy.WindowEnd);
        var windowEnd = Math.Max(policy.WindowStart, policy.WindowEnd);
        var sendAt = Math.Clamp(policy.SendAt, windowStart, windowEnd);
        var daysBefore = date.DayOfWeek == DayOfWeek.Monday && policy.MondayPolicy == "saturday" ? 2 : 1;

        var candidate = date.Date.AddDays(-daysBefore).AddMinutes(sendAt);
        if (candidate <= createdAtLocal)
        {
            candidate = new DateTime(createdAtLocal.Year, createdAtLocal.Month, createdAtLocal.Day, createdAtLocal.Hour, createdAtLocal.Minute, 0)
                .AddMinutes(1);
            var m = (int)candidate.TimeOfDay.TotalMinutes;
            if (m < windowStart) candidate = candidate.Date.AddMinutes(windowStart);
            else if (m > windowEnd) candidate = candidate.Date.AddDays(1).AddMinutes(windowStart);
        }

        // Lembrete que sairia a menos de 2h do horário não ajuda ninguém.
        return candidate > startsAt.AddHours(-2) ? null : candidate;
    }
}

/// <summary>Telefone, placa e formatos de data/hora do contrato.</summary>
public static class AgendaFormat
{
    public static string OnlyDigits(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Telefone digitado → E.164 só com dígitos (acrescenta 55 quando vem só DDD + número).</summary>
    public static string NormalizePhone(string? input)
    {
        var d = OnlyDigits(input);
        return d.Length is 10 or 11 ? "55" + d : d;
    }

    /// <summary>
    /// O WhatsApp ainda entrega alguns celulares brasileiros sem o nono dígito. Para achar o cliente,
    /// as duas formas valem: devolve as variantes a procurar.
    /// </summary>
    public static IReadOnlyList<string> PhoneVariants(string? input)
    {
        var p = NormalizePhone(input);
        if (p.Length == 13 && p.StartsWith("55") && p[4] == '9') return [p, p[..4] + p[5..]];
        if (p.Length == 12 && p.StartsWith("55") && p[4] is >= '6' and <= '9') return [p, p[..4] + "9" + p[4..]];
        return [p];
    }

    public static string NormalizePlate(string? s)
    {
        var p = new string((s ?? string.Empty).ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return p.Length > 7 ? p[..7] : p;
    }

    public static string Date(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime ParseDate(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new AgendaValidationException("Data inválida (use AAAA-MM-DD).");

    /// <summary>'2026-11-07T10:00' (horário local).</summary>
    public static string LocalDateTime(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    public static DateTime ParseLocalDateTime(string? s) =>
        DateTime.TryParseExact(s, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new AgendaValidationException("Data e hora inválidas (use AAAA-MM-DDTHH:mm).");

    public static string Hm(int m) => $"{m / 60:00}:{m % 60:00}";
}

public static class AgendaMapping
{
    public static BookingDto ToDto(this AgendaBooking b) => new(
        b.Id.ToString(),
        AgendaFormat.Date(b.Date),
        b.StartMin,
        b.EndMin,
        b.BoxId,
        b.ServiceId,
        b.ServiceName,
        b.Price,
        b.VehicleType,
        b.Vehicle,
        b.Plate,
        b.ClientName,
        b.Phone,
        b.Notes,
        b.Status,
        b.Origin,
        DateTime.SpecifyKind(b.CreatedAt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        DateTime.SpecifyKind(b.UpdatedAt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        b.RescheduledFrom?.ToString(),
        b.ClientId?.ToString(),
        b.VehicleId?.ToString(),
        b.ClientPlanId?.ToString(),
        BookingExtras.Parse(b.ExtrasJson));

    public static BlockDto ToDto(this AgendaBlock k) =>
        new(k.Id.ToString(), k.BoxId, AgendaFormat.Date(k.Date), k.StartMin, k.EndMin, k.Reason);

    /// <summary><paramref name="planPaid"/>: mensalidades pagas (entram no total gasto); <paramref name="pendingCharges"/>: meses em aberto.</summary>
    public static ClientDto ToDto(
        this AgendaClient c, IReadOnlyList<AgendaClientVehicle> vehicles, ClientStatsRow? stats, IReadOnlyList<string> planNames,
        decimal planPaid, int pendingCharges) => new(
        c.Id.ToString(),
        c.Phone,
        c.Name,
        c.ReminderOptIn,
        c.OptInAt is null ? null : AgendaFormat.LocalDateTime(c.OptInAt.Value),
        c.Vip,
        c.Notes,
        vehicles.Select(v => v.ToDto()).ToList(),
        stats is null
            ? ClientStatsDto.Empty with { TotalSpent = planPaid }
            : new ClientStatsDto(
                stats.Done,
                stats.NoShow,
                stats.Canceled,
                stats.Upcoming,
                stats.TotalSpent + planPaid,
                stats.LastVisit is null ? null : AgendaFormat.Date(stats.LastVisit.Value),
                stats.NextDate is null ? null : AgendaFormat.LocalDateTime(stats.NextDate.Value.AddMinutes(stats.NextStart ?? 0))),
        planNames,
        pendingCharges,
        c.BotBlocked);

    public static ClientVehicleDto ToDto(this AgendaClientVehicle v) =>
        new(v.Id.ToString(), v.Vehicle, v.Plate, v.VehicleType, v.Color);

    public static ReminderDto ToDto(this AgendaReminder r) => new(
        r.Id.ToString(),
        r.BookingId.ToString(),
        r.Type,
        AgendaFormat.LocalDateTime(r.ScheduledFor),
        r.Status,
        r.Attempts,
        r.WamId,
        r.Answer,
        r.AnsweredAt is null ? null : AgendaFormat.LocalDateTime(r.AnsweredAt.Value),
        r.Error);
}
