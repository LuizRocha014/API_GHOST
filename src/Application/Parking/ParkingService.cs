using System.Globalization;
using System.Security.Cryptography;
using Application.Abstractions;
using Application.Agenda;
using Domain.Agenda;

namespace Application.Parking;

/// <summary>
/// Estacionamento rotativo: câmeras, mensalistas, tickets e o que fazer com cada placa lida. Placa de mensalista válido entra
/// e sai sem ticket; outra placa na entrada abre ticket; na saída, o ticket aberto fecha com o valor da tabela.
/// </summary>
public sealed class ParkingService(IParkingRepository repo, ICameraClient cameras, IAgendaClock clock)
{
    public const string IngestRoute = "/api/estacionamento/leitura/";

    private static readonly CultureInfo PtBr = new("pt-BR");

    private static string Money(decimal? v) => (v ?? 0m).ToString("C", PtBr);

    /* ---------- Ajustes ---------- */

    public Task<ParkingSettingsModel> SettingsAsync(Guid branchId, CancellationToken ct = default) => repo.GetSettingsAsync(branchId, ct);

    public async Task<ParkingSettingsModel> SaveSettingsAsync(Guid branchId, ParkingSettingsModel s, CancellationToken ct = default)
    {
        if (s.ToleranceMin is < 0 or > 240) throw new AgendaValidationException("Tolerância de 0 a 240 minutos.");
        if (s.FirstHour < 0 || s.ExtraHour < 0 || s.DailyMax < 0) throw new AgendaValidationException("Os valores não podem ser negativos.");
        if (s.DedupMinutes is < 0 or > 120) throw new AgendaValidationException("Leitura repetida: de 0 a 120 minutos.");
        if (s.MinConfidence is < 0 or > 1) throw new AgendaValidationException("Confiança mínima de 0 a 1.");
        s.PollSeconds = Math.Clamp(s.PollSeconds, 1, 30);
        s.MotionPercent = Math.Clamp(s.MotionPercent, 0.5m, 50m);
        await repo.SaveSettingsAsync(branchId, s, ct);
        return s;
    }

    /* ---------- Câmeras ---------- */

    public CameraDto ToDto(ParkingCamera c)
    {
        var (snapshot, rtsp) = cameras.Urls(c, withPassword: false);
        return new CameraDto(c.Id.ToString(), c.Name, c.Kind, c.Brand, c.Host, c.HttpPort, c.RtspPort, c.Channel, c.Username,
            c.Password.Length > 0, snapshot, rtsp, c.Role, c.AutoRead, IngestRoute + c.IngestToken,
            c.LastSeenAt is { } s ? ParkingFormat.Local(clock.ToLocal(s)) : null, c.LastError);
    }

    public async Task<IReadOnlyList<CameraDto>> ListCamerasAsync(Guid branchId, CancellationToken ct = default) =>
        (await repo.ListCamerasAsync(branchId, ct)).Select(ToDto).ToList();

    public async Task<ParkingCamera> CameraAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await repo.GetCameraAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Câmera não encontrada.");

    public async Task<CameraDto> SaveCameraAsync(Guid branchId, Guid? id, CameraRequest req, CancellationToken ct = default)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (name.Length == 0) throw new AgendaValidationException("Dê um nome para a câmera (ex.: Portão de entrada).");
        if (!CameraBrands.All.Contains(req.Brand)) throw new AgendaValidationException("Marca inválida.");
        if (!CameraRoles.All.Contains(req.Role)) throw new AgendaValidationException("Uso inválido (nenhum, entrada ou saída).");
        if (req.Kind is not (CameraKinds.Ip or CameraKinds.Dvr)) throw new AgendaValidationException("Tipo inválido (câmera IP ou DVR).");
        var host = (req.Host ?? string.Empty).Trim();
        if (req.Brand != CameraBrands.Generic && host.Length == 0) throw new AgendaValidationException("Informe o IP da câmera ou do DVR.");
        if (req.Brand == CameraBrands.Generic && string.IsNullOrWhiteSpace(req.SnapshotUrl) && string.IsNullOrWhiteSpace(req.RtspUrl))
            throw new AgendaValidationException("Na marca \"outra\", informe o endereço da imagem (snapshot) ou do vídeo (RTSP).");
        if (req.Channel is < 1 or > 256) throw new AgendaValidationException("Canal de 1 a 256.");
        if (req.HttpPort is < 1 or > 65535 || req.RtspPort is < 1 or > 65535) throw new AgendaValidationException("Porta inválida.");

        var utc = DateTime.UtcNow;
        var c = id is { } existing ? await CameraAsync(branchId, existing, ct) : new ParkingCamera
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            IngestToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            CreatedAt = utc,
            Active = true,
        };
        c.Name = name;
        c.Kind = req.Kind;
        c.Brand = req.Brand;
        c.Host = host;
        c.HttpPort = req.HttpPort;
        c.RtspPort = req.RtspPort;
        c.Channel = req.Channel;
        c.Username = (req.Username ?? string.Empty).Trim();
        if (req.Password is not null) c.Password = req.Password;
        c.SnapshotUrl = (req.SnapshotUrl ?? string.Empty).Trim();
        c.RtspUrl = (req.RtspUrl ?? string.Empty).Trim();
        c.Role = req.Role;
        c.AutoRead = req.AutoRead && req.Role != CameraRoles.None;
        c.UpdatedAt = utc;
        await repo.SaveCameraAsync(c, ct);
        return ToDto(c);
    }

    public async Task DeleteCameraAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await repo.DeleteCameraAsync(await CameraAsync(branchId, id, ct), ct);

    /// <summary>Imagem da câmera agora. Marca quando respondeu (ou o erro) para a tela de câmeras.</summary>
    public async Task<byte[]> SnapshotAsync(ParkingCamera c, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cameras.SnapshotAsync(c, ct);
            c.LastSeenAt = DateTime.UtcNow;
            c.LastError = null;
            await repo.SaveCameraAsync(c, ct);
            return bytes;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            c.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await repo.SaveCameraAsync(c, ct);
            throw new AgendaValidationException($"A câmera não respondeu: {c.LastError}");
        }
    }

    /* ---------- Mensalistas ---------- */

    private bool Valid(ParkingMonthly m) => m.Active && (m.ValidUntil is null || m.ValidUntil.Value.Date >= clock.NowLocal.Date);

    private MonthlyDto ToDto(ParkingMonthly m) => new(
        m.Id.ToString(), m.Plate, m.Name, m.Phone, m.Vehicle, m.ValidUntil is { } v ? AgendaFormat.Date(v) : null, m.MonthlyFee,
        m.Notes, m.Active, Valid(m));

    public async Task<IReadOnlyList<MonthlyDto>> ListMonthliesAsync(Guid branchId, CancellationToken ct = default) =>
        (await repo.ListMonthliesAsync(branchId, ct)).OrderBy(m => m.Name).Select(ToDto).ToList();

    public async Task<MonthlyDto> SaveMonthlyAsync(Guid branchId, Guid? id, MonthlyRequest req, CancellationToken ct = default)
    {
        var plate = ParkingRules.NormalizePlate(req.Plate);
        if (!ParkingRules.IsValidPlate(plate)) throw new AgendaValidationException("Placa inválida (ex.: ABC1D23 ou ABC1234).");
        var name = (req.Name ?? string.Empty).Trim();
        if (name.Length == 0) throw new AgendaValidationException("Informe o nome do mensalista.");
        var other = await repo.FindMonthlyAsync(branchId, plate, ct);
        if (other is not null && other.Id != id) throw new AgendaValidationException($"A placa {plate} já é de {other.Name}.");
        var utc = DateTime.UtcNow;
        var m = id is { } existing
            ? await repo.GetMonthlyAsync(branchId, existing, ct) ?? throw new AgendaNotFoundException("Mensalista não encontrado.")
            : new ParkingMonthly { Id = Guid.NewGuid(), BranchId = branchId, CreatedAt = utc };
        m.Plate = plate;
        m.Name = name;
        m.Phone = AgendaFormat.NormalizePhone(req.Phone);
        m.Vehicle = (req.Vehicle ?? string.Empty).Trim();
        m.ValidUntil = string.IsNullOrWhiteSpace(req.ValidUntil) ? null : AgendaFormat.ParseDate(req.ValidUntil);
        m.MonthlyFee = req.MonthlyFee;
        m.Notes = (req.Notes ?? string.Empty).Trim();
        m.Active = req.Active;
        m.UpdatedAt = utc;
        await repo.SaveMonthlyAsync(m, ct);
        return ToDto(m);
    }

    public async Task DeleteMonthlyAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await repo.DeleteMonthlyAsync(await repo.GetMonthlyAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Mensalista não encontrado."), ct);

    /* ---------- Tickets ---------- */

    private TicketDto ToDto(ParkingTicket t, ParkingSettingsModel s, IReadOnlyDictionary<Guid, string> cams)
    {
        var now = clock.NowLocal;
        var end = t.ExitAt ?? now;
        return new TicketDto(
            t.Id.ToString(), t.Number, t.Plate, t.Vehicle, ParkingFormat.Local(t.EntryAt), t.ExitAt is { } x ? ParkingFormat.Local(x) : null,
            t.Status, t.Amount, t.Status == TicketStatuses.Open ? ParkingRules.Price(s, t.EntryAt, now) : t.Amount,
            ParkingRules.Duration(t.EntryAt, end), t.PaidAmount, t.PaymentMethod, t.PaidAt is { } p ? ParkingFormat.Local(p) : null,
            t.EntryCameraId is { } ec && cams.TryGetValue(ec, out var en) ? en : null,
            t.ExitCameraId is { } xc && cams.TryGetValue(xc, out var xn) ? xn : null, t.Notes);
    }

    private async Task<Dictionary<Guid, string>> CameraNamesAsync(Guid branchId, CancellationToken ct) =>
        (await repo.ListCamerasAsync(branchId, ct)).ToDictionary(c => c.Id, c => c.Name);

    public async Task<IReadOnlyList<TicketDto>> ListTicketsAsync(Guid branchId, string? from, string? to, bool onlyOpen, CancellationToken ct = default)
    {
        var s = await repo.GetSettingsAsync(branchId, ct);
        var cams = await CameraNamesAsync(branchId, ct);
        DateTime? f = string.IsNullOrWhiteSpace(from) ? null : AgendaFormat.ParseDate(from);
        DateTime? t = string.IsNullOrWhiteSpace(to) ? null : AgendaFormat.ParseDate(to).AddDays(1);
        return (await repo.ListTicketsAsync(branchId, f, t, onlyOpen, ct)).Select(x => ToDto(x, s, cams)).ToList();
    }

    public async Task<TicketDto> OpenTicketAsync(Guid branchId, OpenTicketRequest req, CancellationToken ct = default)
    {
        var r = await ProcessReadingAsync(branchId, null, req.Plate, null, ReadingSources.Manual, CameraRoles.Entry, null, ct,
            vehicle: req.Vehicle, notes: req.Notes, forceTicket: true);
        if (r.TicketId is null) throw new AgendaValidationException(r.Message);
        return await TicketAsync(branchId, Guid.Parse(r.TicketId), ct);
    }

    public async Task<TicketDto> TicketAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var t = await repo.GetTicketAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Ticket não encontrado.");
        return ToDto(t, await repo.GetSettingsAsync(branchId, ct), await CameraNamesAsync(branchId, ct));
    }

    /// <summary>Fecha o ticket (saída) e, com a forma de pagamento, já registra o recebimento. Ticket fechado sem pagamento recebe depois.</summary>
    public async Task<TicketDto> CloseTicketAsync(Guid branchId, Guid id, CloseTicketRequest req, CancellationToken ct = default)
    {
        var t = await repo.GetTicketAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Ticket não encontrado.");
        if (t.Status == TicketStatuses.Canceled) throw new AgendaValidationException("Ticket cancelado.");
        var s = await repo.GetSettingsAsync(branchId, ct);
        var now = clock.NowLocal;
        if (t.Status == TicketStatuses.Open)
        {
            t.ExitAt = now;
            t.Amount = ParkingRules.Price(s, t.EntryAt, now);
            t.Status = TicketStatuses.Closed;
        }
        if (!string.IsNullOrWhiteSpace(req.Method))
        {
            if (!PaymentMethods.All.Contains(req.Method)) throw new AgendaValidationException("Forma de pagamento inválida.");
            if (req.Amount is < 0) throw new AgendaValidationException("Valor inválido.");
            t.PaymentMethod = req.Method;
            t.PaidAmount = req.Amount ?? t.Amount ?? 0m;
            t.PaidAt = now;
        }
        t.UpdatedAt = DateTime.UtcNow;
        await repo.UpdateTicketAsync(t, ct);
        return ToDto(t, s, await CameraNamesAsync(branchId, ct));
    }

    public async Task<TicketDto> CancelTicketAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var t = await repo.GetTicketAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Ticket não encontrado.");
        if (t.PaidAt is not null) throw new AgendaValidationException("Ticket já pago não pode ser cancelado.");
        t.Status = TicketStatuses.Canceled;
        t.ExitAt ??= clock.NowLocal;
        t.UpdatedAt = DateTime.UtcNow;
        await repo.UpdateTicketAsync(t, ct);
        return ToDto(t, await repo.GetSettingsAsync(branchId, ct), await CameraNamesAsync(branchId, ct));
    }

    /* ---------- Leituras ---------- */

    private static string Message(ParkingReading r, ParkingTicket? t, ParkingMonthly? m) => r.Result switch
    {
        "monthly" => $"Mensalista {m?.Name}: {(r.Direction == CameraRoles.Exit ? "saída" : "entrada")} liberada.",
        "ticket_opened" => $"Ticket nº {t?.Number} aberto para {r.Plate}.",
        "expired_monthly" => $"Mensalidade de {m?.Name} vencida: ticket nº {t?.Number} aberto.",
        "ticket_closed" => $"Ticket nº {t?.Number} fechado: {Money(t?.Amount)} ({(t is null ? "" : ParkingRules.Duration(t.EntryAt, t.ExitAt ?? t.EntryAt))}).",
        "already_open" => $"{r.Plate} já tem o ticket nº {t?.Number} aberto.",
        "no_ticket" => $"Saída de {r.Plate} sem ticket aberto.",
        "duplicate" => $"Leitura repetida de {r.Plate} ignorada.",
        "low_confidence" => $"Leitura com pouca confiança ({r.Confidence:P0}): confira a placa {r.Plate}.",
        "invalid_plate" => $"Placa não reconhecida: \"{r.Plate}\".",
        "disabled" => "Estacionamento desligado nos ajustes.",
        _ => r.Result,
    };

    /// <summary>
    /// O que fazer com uma placa lida: entrada de mensalista válido só registra; outra placa abre ticket (se já não tiver um
    /// aberto); na saída, fecha o ticket aberto com o valor da tabela. Leitura repetida e de pouca confiança só ficam no registro.
    /// </summary>
    public async Task<ReadingDto> ProcessReadingAsync(
        Guid branchId, ParkingCamera? camera, string rawPlate, decimal? confidence, string source, string? direction, byte[]? image,
        CancellationToken ct = default, string? vehicle = null, string? notes = null, bool forceTicket = false)
    {
        var s = await repo.GetSettingsAsync(branchId, ct);
        var now = clock.NowLocal;
        var plate = ParkingRules.NormalizePlate(rawPlate);
        var dir = direction is CameraRoles.Entry or CameraRoles.Exit ? direction
            : camera?.Role is CameraRoles.Entry or CameraRoles.Exit ? camera.Role : CameraRoles.Entry;
        var reading = new ParkingReading
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            CameraId = camera?.Id,
            Plate = plate.Length == 0 ? (rawPlate ?? string.Empty).Trim() : plate,
            Confidence = confidence,
            Source = source,
            Direction = dir,
            Image = image,
            At = now,
        };
        ParkingTicket? ticket = null;
        ParkingMonthly? monthly = null;

        if (!s.Enabled && !forceTicket) reading.Result = "disabled";
        else if (!ParkingRules.IsValidPlate(plate))
        {
            if (forceTicket) throw new AgendaValidationException("Placa inválida (ex.: ABC1D23 ou ABC1234).");
            reading.Result = "invalid_plate";
        }
        else if (!forceTicket && camera is not null && s.DedupMinutes > 0
                 && await repo.LastReadingAsync(branchId, camera.Id, plate, ct) is { } last
                 && last.Result != "duplicate" && (now - last.At).TotalMinutes < s.DedupMinutes)
        {
            reading.Result = "duplicate";
            reading.Image = null;
        }
        else if (confidence is { } c && c < s.MinConfidence) reading.Result = "low_confidence";
        else
        {
            monthly = await repo.FindMonthlyAsync(branchId, plate, ct);
            var open = await repo.FindOpenTicketAsync(branchId, plate, ct);
            if (dir == CameraRoles.Exit)
            {
                if (open is not null)
                {
                    open.ExitAt = now;
                    open.ExitCameraId = camera?.Id;
                    open.Amount = ParkingRules.Price(s, open.EntryAt, now);
                    open.Status = TicketStatuses.Closed;
                    open.UpdatedAt = DateTime.UtcNow;
                    await repo.UpdateTicketAsync(open, ct);
                    ticket = open;
                    reading.Result = "ticket_closed";
                }
                else reading.Result = monthly is not null && Valid(monthly) ? "monthly" : "no_ticket";
            }
            else if (monthly is not null && Valid(monthly) && !forceTicket) reading.Result = "monthly";
            else if (open is not null)
            {
                ticket = open;
                reading.Result = "already_open";
            }
            else
            {
                var utc = DateTime.UtcNow;
                ticket = new ParkingTicket
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    Plate = plate,
                    Vehicle = (vehicle ?? monthly?.Vehicle ?? string.Empty).Trim(),
                    EntryAt = now,
                    EntryCameraId = camera?.Id,
                    Status = TicketStatuses.Open,
                    Notes = (notes ?? string.Empty).Trim(),
                    CreatedAt = utc,
                    UpdatedAt = utc,
                };
                await repo.InsertTicketAsync(ticket, ct);
                reading.Result = monthly is not null ? "expired_monthly" : "ticket_opened";
            }
        }
        reading.TicketId = ticket?.Id;
        reading.MonthlyId = monthly?.Id;
        await repo.InsertReadingAsync(reading, ct);
        return new ReadingDto(reading.Id.ToString(), ParkingFormat.Local(reading.At), reading.Plate, reading.Confidence, reading.Source,
            reading.Direction, reading.Result, Message(reading, ticket, monthly), camera?.Name, ticket?.Id.ToString(), ticket?.Number,
            monthly?.Name, reading.Image is { Length: > 0 });
    }

    public async Task<IReadOnlyList<ReadingDto>> ListReadingsAsync(Guid branchId, int days, int limit, CancellationToken ct = default)
    {
        var from = clock.NowLocal.Date.AddDays(-Math.Clamp(days, 0, 90));
        var readings = await repo.ListReadingsAsync(branchId, from, Math.Clamp(limit, 1, 500), ct);
        var cams = await CameraNamesAsync(branchId, ct);
        var tickets = (await repo.ListTicketsAsync(branchId, from, null, false, ct)).ToDictionary(t => t.Id);
        var monthlies = (await repo.ListMonthliesAsync(branchId, ct)).ToDictionary(m => m.Id);
        return readings.Select(r =>
        {
            var t = r.TicketId is { } ti && tickets.TryGetValue(ti, out var tt) ? tt : null;
            var m = r.MonthlyId is { } mi && monthlies.TryGetValue(mi, out var mm) ? mm : null;
            return new ReadingDto(r.Id.ToString(), ParkingFormat.Local(r.At), r.Plate, r.Confidence, r.Source, r.Direction, r.Result,
                Message(r, t, m), r.CameraId is { } ci && cams.TryGetValue(ci, out var cn) ? cn : null, r.TicketId?.ToString(), t?.Number,
                m?.Name, r.Image is { Length: > 0 });
        }).ToList();
    }

    public Task<byte[]?> ReadingImageAsync(Guid branchId, Guid id, CancellationToken ct = default) => repo.ReadingImageAsync(branchId, id, ct);

    /// <summary>Pátio agora: tickets abertos (com o valor até agora), mensalistas dentro e o movimento do dia.</summary>
    public async Task<YardDto> YardAsync(Guid branchId, CancellationToken ct = default)
    {
        var today = clock.NowLocal.Date;
        var open = await ListTicketsAsync(branchId, null, null, true, ct);
        var readings = await ListReadingsAsync(branchId, 0, 500, ct);
        var counted = readings.Where(r => r.Result is "monthly" or "ticket_opened" or "expired_monthly" or "ticket_closed").ToList();
        var monthlyInside = readings.Where(r => r.Result == "monthly").GroupBy(r => r.Plate)
            .Count(g => g.OrderByDescending(r => r.At).First().Direction == CameraRoles.Entry);
        var paidToday = (await repo.ListTicketsAsync(branchId, today.AddDays(-30), null, false, ct))
            .Where(t => t.PaidAt?.Date == today).Sum(t => t.PaidAmount ?? 0m);
        return new YardDto(open, monthlyInside, counted.Count(r => r.Direction == CameraRoles.Entry),
            counted.Count(r => r.Direction == CameraRoles.Exit), paidToday, readings.Take(30).ToList());
    }
}
