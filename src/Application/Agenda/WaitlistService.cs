using Application.Abstractions;
using Domain.Agenda;
using Microsoft.Extensions.Logging;

namespace Application.Agenda;

/// <summary>Pedido na lista de espera. <c>Status</c>: waiting, notified, booked, canceled, expired ou failed.</summary>
public sealed record WaitlistEntryDto(
    string Id,
    string Date,
    string ServiceId,
    string ServiceName,
    string VehicleType,
    IReadOnlyList<string> ExtraIds,
    string ClientName,
    string Phone,
    string? ClientId,
    string Origin,
    string Status,
    string CreatedAt,
    string? NotifiedAt,
    string? Error);

/// <summary>Pôr na lista de espera: dia, serviço (com tipo de veículo e adicionais) e o cliente.</summary>
public sealed class WaitlistRequest
{
    public string Date { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public List<string>? ExtraIds { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Origin { get; set; }
}

/// <summary>Aviso de vaga pelo WhatsApp (modelo "vaga_disponivel", botão Ver horários = wl:{id}).</summary>
public interface IWaitlistNoticeSender
{
    Task<string> SendAsync(Guid branchId, AgendaWaitlistEntry entry, IReadOnlyDictionary<string, string> values, AgendaConfigModel config,
        CancellationToken ct = default);
}

/// <summary>
/// Lista de espera: quem queria horário num dia lotado. <see cref="NotifyAsync"/> (chamado pelo worker a cada minuto) confere
/// a agenda de cada pedido aberto e avisa pelo WhatsApp quando abre um horário que cabe no serviço; também marca como
/// "agendou" quem marcou horário no dia e como "expirou" os dias que passaram.
/// </summary>
public sealed class WaitlistService(
    IAgendaRepository repo, IAgendaService agenda, IAgendaClock clock, IWaitlistNoticeSender sender, ILogger<WaitlistService> logger)
{
    public const int MaxAttempts = 3;

    public static WaitlistEntryDto ToDto(AgendaWaitlistEntry e, AgendaConfigModel cfg) => new(
        e.Id.ToString(),
        AgendaFormat.Date(e.Date),
        e.ServiceId,
        cfg.ServiceById(e.ServiceId)?.Name ?? e.ServiceId,
        e.VehicleType,
        SplitIds(e.ExtraIds),
        e.ClientName,
        e.Phone,
        e.ClientId?.ToString(),
        e.Origin,
        e.Status,
        Utc(e.CreatedAt),
        e.NotifiedAt is { } n ? Utc(n) : null,
        e.Error);

    private static string Utc(DateTime d) =>
        DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    public static IReadOnlyList<string> SplitIds(string ids) =>
        ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task<AgendaConfigModel> ConfigAsync(Guid branchId, CancellationToken ct) =>
        AgendaConfigModel.Parse(await agenda.GetConfigAsync(branchId, ct));

    public async Task<IReadOnlyList<WaitlistEntryDto>> ListAsync(Guid branchId, string from, string to, CancellationToken ct = default)
    {
        var cfg = await ConfigAsync(branchId, ct);
        return (await repo.ListWaitlistAsync(branchId, AgendaFormat.ParseDate(from), AgendaFormat.ParseDate(to), ct)).Select(e => ToDto(e, cfg)).ToList();
    }

    public async Task<AgendaWaitlistEntry?> GetAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await repo.GetWaitlistAsync(branchId, id, ct);

    /// <summary>
    /// Põe na lista (ou atualiza o pedido aberto do mesmo número no mesmo dia: um pedido por pessoa e dia).
    /// </summary>
    public async Task<WaitlistEntryDto> AddAsync(Guid branchId, WaitlistRequest req, CancellationToken ct = default)
    {
        var cfg = await ConfigAsync(branchId, ct);
        var date = AgendaFormat.ParseDate(req.Date);
        if (date < clock.NowLocal.Date) throw new AgendaValidationException("Esse dia já passou.");
        var svc = cfg.ServiceById(req.ServiceId);
        if (svc is not { Active: true }) throw new AgendaValidationException("Escolha um serviço ativo.");
        if (!AgendaConfigModel.AllVehicleTypes.Contains(req.VehicleType) || !cfg.VehicleEnabled(req.VehicleType)) throw new AgendaValidationException("Tipo de veículo inválido.");
        var phone = AgendaFormat.NormalizePhone(req.Phone);
        if (phone.Length < 12) throw new AgendaValidationException("Informe o WhatsApp com DDD: é por ele que o cliente é avisado.");
        var name = (req.ClientName ?? string.Empty).Trim();
        if (name.Length == 0) throw new AgendaValidationException("Informe o nome do cliente.");
        var extras = string.Join(',', (req.ExtraIds ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        if (extras.Length > 500) throw new AgendaValidationException("Adicionais demais.");

        var client = await agenda.GetClientAsync(branchId, phone, ct);
        var utc = DateTime.UtcNow;
        var variants = AgendaFormat.PhoneVariants(phone);
        var same = (await repo.ListOpenWaitlistAsync(branchId, ct)).FirstOrDefault(e => e.Date == date && variants.Contains(e.Phone));
        if (same is not null)
        {
            same.ServiceId = svc.Id;
            same.VehicleType = req.VehicleType;
            same.ExtraIds = extras;
            same.ClientName = name;
            same.ClientId = client is null ? null : Guid.Parse(client.Id);
            // Pediu de novo: volta a esperar (pode ter sido avisado de uma vaga que já foi ocupada).
            same.Status = WaitlistStatuses.Waiting;
            same.Attempts = 0;
            same.Error = null;
            same.UpdatedAt = utc;
            await repo.UpdateWaitlistAsync(same, ct);
            return ToDto(same, cfg);
        }

        var entry = new AgendaWaitlistEntry
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Date = date,
            ServiceId = svc.Id,
            VehicleType = req.VehicleType,
            ExtraIds = extras,
            ClientName = name,
            Phone = phone,
            ClientId = client is null ? null : Guid.Parse(client.Id),
            Origin = req.Origin == BookingOrigins.WhatsApp ? BookingOrigins.WhatsApp : BookingOrigins.Counter,
            Status = WaitlistStatuses.Waiting,
            CreatedAt = utc,
            UpdatedAt = utc,
        };
        await repo.InsertWaitlistAsync(entry, ct);
        return ToDto(entry, cfg);
    }

    public async Task CancelAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var e = await repo.GetWaitlistAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Pedido da lista de espera não encontrado.");
        if (!WaitlistStatuses.IsOpen(e.Status)) return;
        e.Status = WaitlistStatuses.Canceled;
        e.UpdatedAt = DateTime.UtcNow;
        await repo.UpdateWaitlistAsync(e, ct);
    }

    /// <summary>Marca como "agendou" o pedido aberto do número nesse dia (o cliente marcou horário).</summary>
    public async Task MarkBookedAsync(Guid branchId, string phone, string date, CancellationToken ct = default)
    {
        var d = AgendaFormat.ParseDate(date);
        var variants = AgendaFormat.PhoneVariants(phone);
        foreach (var e in (await repo.ListOpenWaitlistAsync(branchId, ct)).Where(e => e.Date == d && variants.Contains(e.Phone)))
        {
            e.Status = WaitlistStatuses.Booked;
            e.UpdatedAt = DateTime.UtcNow;
            await repo.UpdateWaitlistAsync(e, ct);
        }
    }

    /// <summary>
    /// Confere os pedidos abertos da filial: avisa quem tem vaga agora, marca quem já marcou horário e expira os dias que
    /// passaram. Devolve quantos pedidos mudaram. Com o aviso desligado em Ajustes, só faz a arrumação.
    /// </summary>
    public async Task<int> NotifyAsync(Guid branchId, CancellationToken ct = default)
    {
        var open = await repo.ListOpenWaitlistAsync(branchId, ct);
        if (open.Count == 0) return 0;
        var cfg = await ConfigAsync(branchId, ct);
        var now = clock.NowLocal;
        var changed = 0;
        // Horário marcado no dia pelo mesmo número, depois do pedido: conseguiu a vaga.
        var dates = open.Select(e => e.Date).Where(d => d >= now.Date).Distinct().ToList();
        var bookingsByDate = new Dictionary<DateTime, IReadOnlyList<BookingDto>>();
        foreach (var d in dates)
            bookingsByDate[d] = await agenda.ListBookingsAsync(branchId, AgendaFormat.Date(d), AgendaFormat.Date(d), ct);

        foreach (var e in open)
        {
            var utc = DateTime.UtcNow;
            if (e.Date < now.Date)
            {
                e.Status = WaitlistStatuses.Expired;
                e.UpdatedAt = utc;
                await repo.UpdateWaitlistAsync(e, ct);
                changed++;
                continue;
            }
            var variants = AgendaFormat.PhoneVariants(e.Phone);
            var booked = bookingsByDate[e.Date].Any(b =>
                variants.Contains(b.Phone) && b.Status is not (BookingStatuses.Canceled or BookingStatuses.NoShow)
                && DateTime.TryParse(b.CreatedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var created)
                && created >= e.CreatedAt.AddSeconds(-5));
            if (booked)
            {
                e.Status = WaitlistStatuses.Booked;
                e.UpdatedAt = utc;
                await repo.UpdateWaitlistAsync(e, ct);
                changed++;
                continue;
            }
            if (e.Status != WaitlistStatuses.Waiting || !cfg.Notify.Waitlist) continue;
            // Avisos só de dia (8h às 21h): ninguém quer mensagem de vaga de madrugada.
            if (now.Hour is < 8 or >= 21) continue;

            var slots = await agenda.AvailabilityAsync(
                branchId, AgendaFormat.Date(e.Date), e.ServiceId, e.VehicleType, null, cfg.Bot.MinNoticeMin, SplitIds(e.ExtraIds), ct);
            if (slots.Count == 0) continue;

            e.Attempts += 1;
            try
            {
                var values = Values(cfg, e, slots[0].Start);
                e.WamId = await sender.SendAsync(branchId, e, values, cfg, ct);
                e.Status = WaitlistStatuses.Notified;
                e.NotifiedAt = utc;
                e.Error = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Lista de espera: não foi possível avisar {Phone} ({Id}).", e.Phone, e.Id);
                e.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (e.Attempts >= MaxAttempts) e.Status = WaitlistStatuses.Failed;
            }
            e.UpdatedAt = utc;
            await repo.UpdateWaitlistAsync(e, ct);
            changed++;
        }
        return changed;
    }

    /// <summary>Variáveis do aviso de vaga: {nome}, {estetica}, {data}, {servico}, {hora} (primeiro horário livre).</summary>
    public static Dictionary<string, string> Values(AgendaConfigModel cfg, AgendaWaitlistEntry e, int firstStart)
    {
        var first = (e.ClientName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return new Dictionary<string, string>
        {
            ["nome"] = first.Length == 0 ? "cliente" : first,
            ["estetica"] = cfg.BusinessName,
            ["data"] = Bot.BotFormat.DayLabel(AgendaFormat.Date(e.Date)),
            ["servico"] = cfg.ServiceById(e.ServiceId)?.Name ?? string.Empty,
            ["hora"] = AgendaFormat.Hm(firstStart),
        };
    }
}
