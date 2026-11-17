using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Abstractions;
using Domain.Agenda;

namespace Application.Agenda;

public interface IAgendaService
{
    Task<IReadOnlyList<AgendaBranchDto>> BranchesAsync(Guid userId, CancellationToken ct = default);
    Task EnsureAccessAsync(Guid userId, Guid branchId, CancellationToken ct = default);

    Task<string> GetConfigAsync(Guid branchId, CancellationToken ct = default);
    Task<string> SaveConfigAsync(Guid branchId, string json, CancellationToken ct = default);

    Task<IReadOnlyList<BookingDto>> ListBookingsAsync(Guid branchId, string from, string to, CancellationToken ct = default);
    Task<BookingDto> GetBookingAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SlotOptionDto>> AvailabilityAsync(Guid branchId, string date, string serviceId, string vehicleType, Guid? ignoreBookingId, int minNoticeMin, CancellationToken ct = default);
    Task<BookingDto> CreateBookingAsync(Guid branchId, NewBookingRequest request, CancellationToken ct = default);
    Task<BookingDto> SetStatusAsync(Guid branchId, Guid id, string status, CancellationToken ct = default);
    Task<BookingDto> RescheduleAsync(Guid branchId, Guid id, RescheduleRequest request, CancellationToken ct = default);
    Task DeleteBookingAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<int> PruneAsync(Guid branchId, string before, CancellationToken ct = default);

    Task<IReadOnlyList<BlockDto>> ListBlocksAsync(Guid branchId, string from, string to, CancellationToken ct = default);
    Task<BlockDto> CreateBlockAsync(Guid branchId, NewBlockRequest request, CancellationToken ct = default);
    Task DeleteBlockAsync(Guid branchId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReminderDto>> ListRemindersAsync(Guid branchId, string from, string to, CancellationToken ct = default);
    Task<IReadOnlyList<ReminderDispatchDto>> DispatchRemindersAsync(Guid branchId, DispatchRequest request, CancellationToken ct = default);

    /// <summary>Filiais com lembrete vencido agora (o worker chama <see cref="DispatchRemindersAsync"/> em cada uma).</summary>
    Task<IReadOnlyList<Guid>> BranchesWithDueRemindersAsync(CancellationToken ct = default);
    Task MarkDeliveryAsync(Guid branchId, DeliveryRequest request, CancellationToken ct = default);
    Task<ReminderDto?> RegisterAnswerAsync(Guid branchId, Guid bookingId, AnswerRequest request, CancellationToken ct = default);

    Task<ClientDto?> GetClientAsync(Guid branchId, string phone, CancellationToken ct = default);
    Task<IReadOnlyList<ClientDto>> ListClientsAsync(Guid branchId, CancellationToken ct = default);
    Task<ClientDto> UpsertClientAsync(Guid branchId, string phone, ClientRequest request, CancellationToken ct = default);
}

public sealed class AgendaService(IAgendaRepository repo, IAgendaClock clock, IReminderSender sender) : IAgendaService
{
    private static readonly string[] VehicleTypes = ["Hatch", "Sedan", "SUV", "Picape", "Moto"];
    private const int MaxReminderAttempts = 3;

    // Tamanhos das colunas (AppDbContext): acima disso a API responde 400 em vez de estourar no banco.
    public const int MaxIdLength = 64;
    public const int MaxNameLength = 120;
    public const int MaxVehicleLength = 80;
    public const int MaxNotesLength = 1000;
    public const int MaxReasonLength = 200;
    public const int MaxPhoneLength = 15;
    public const int MaxErrorLength = 1000;

    private static string Truncate(string text, int max) => text.Length > max ? text[..max] : text;

    private static string LimitedText(string? value, int max, string field)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length > max) throw new AgendaValidationException($"{field}: use no máximo {max} caracteres.");
        return text;
    }

    /* ---------- Acesso ---------- */

    public Task<IReadOnlyList<AgendaBranchDto>> BranchesAsync(Guid userId, CancellationToken ct = default) =>
        repo.ListBranchesForUserAsync(userId, ct);

    public async Task EnsureAccessAsync(Guid userId, Guid branchId, CancellationToken ct = default)
    {
        if (!await repo.CanAccessBranchAsync(userId, branchId, ct)) throw new AgendaForbiddenException();
    }

    /* ---------- Configuração ---------- */

    private async Task<(string Json, AgendaConfigModel Model)> LoadConfigAsync(Guid branchId, CancellationToken ct)
    {
        var json = await repo.GetConfigJsonAsync(branchId, ct);
        if (json is null)
        {
            // Filial abrindo a agenda pela primeira vez: começa com a configuração padrão.
            var name = await repo.GetBranchNameAsync(branchId, ct) ?? throw new AgendaNotFoundException("Filial não encontrada.");
            json = AgendaConfigModel.DefaultJson(name);
            await repo.SaveConfigJsonAsync(branchId, json, ct);
        }
        return (json, AgendaConfigModel.Parse(json));
    }

    public async Task<string> GetConfigAsync(Guid branchId, CancellationToken ct = default) => (await LoadConfigAsync(branchId, ct)).Json;

    public async Task<string> SaveConfigAsync(Guid branchId, string json, CancellationToken ct = default)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            throw new AgendaValidationException("Configuração inválida (JSON malformado).");
        }
        if (node is not JsonObject) throw new AgendaValidationException("Configuração inválida.");

        AgendaConfigModel model;
        try
        {
            model = AgendaConfigModel.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new AgendaValidationException($"Configuração inválida: {ex.Message}");
        }

        if (model.Boxes.Count == 0) throw new AgendaValidationException("Cadastre pelo menos um box.");
        if (model.Hours.Count != 7) throw new AgendaValidationException("O horário de funcionamento precisa ter os 7 dias da semana.");
        string[] days = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];
        for (var i = 0; i < 7; i++)
        {
            var h = model.Hours[i];
            if (h is not null && (h.Length != 2 || h[1] <= h[0] || h[0] < 0 || h[1] > 1440))
                throw new AgendaValidationException($"Horário inválido na {days[i]}: fechar precisa ser depois de abrir.");
        }
        if (model.Step <= 0 || model.Step > 240) throw new AgendaValidationException("Intervalo entre horários inválido.");
        if (model.Boxes.Select(b => b.Id).Distinct().Count() != model.Boxes.Count) throw new AgendaValidationException("Há boxes com o mesmo id.");
        if (model.Services.Select(s => s.Id).Distinct().Count() != model.Services.Count) throw new AgendaValidationException("Há serviços com o mesmo id.");
        foreach (var b in model.Boxes)
        {
            if (string.IsNullOrWhiteSpace(b.Id) || b.Id.Length > MaxIdLength) throw new AgendaValidationException("Box com id inválido.");
            LimitedText(b.Name, MaxNameLength, "Nome do box");
        }
        foreach (var s in model.Services)
        {
            if (string.IsNullOrWhiteSpace(s.Id) || s.Id.Length > MaxIdLength) throw new AgendaValidationException("Serviço com id inválido.");
            LimitedText(s.Name, MaxNameLength, "Nome do serviço");
        }

        var normalized = node.ToJsonString(AgendaConfigModel.Json);
        await repo.SaveConfigJsonAsync(branchId, normalized, ct);
        return normalized;
    }

    /* ---------- Agendamentos ---------- */

    public async Task<IReadOnlyList<BookingDto>> ListBookingsAsync(Guid branchId, string from, string to, CancellationToken ct = default)
    {
        var list = await repo.ListBookingsAsync(branchId, AgendaFormat.ParseDate(from), AgendaFormat.ParseDate(to), ct);
        return list.Select(b => b.ToDto()).ToList();
    }

    public async Task<BookingDto> GetBookingAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        (await repo.GetBookingAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Agendamento não encontrado.")).ToDto();

    private static IReadOnlyList<BusyItem> Busy(IEnumerable<AgendaBooking> bookings) =>
        bookings.Where(b => BookingStatuses.IsActive(b.Status)).Select(b => new BusyItem(b.Id, b.BoxId, b.StartMin, b.EndMin)).ToList();

    private static IReadOnlyList<BusyItem> Busy(IEnumerable<AgendaBlock> blocks) =>
        blocks.Select(k => new BusyItem(null, k.BoxId, k.StartMin, k.EndMin)).ToList();

    public async Task<IReadOnlyList<SlotOptionDto>> AvailabilityAsync(
        Guid branchId, string date, string serviceId, string vehicleType, Guid? ignoreBookingId, int minNoticeMin, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var day = AgendaFormat.ParseDate(date);
        var service = cfg.ServiceById(serviceId);
        if (service is null || !service.Active) return [];
        var bookings = await repo.ListBookingsAsync(branchId, day, day, ct);
        var blocks = await repo.ListBlocksAsync(branchId, day, day, ct);
        return AvailabilityRules.AvailableStarts(
            cfg, day, service.Id, service.DurationFor(vehicleType), Busy(bookings), Busy(blocks), clock.NowLocal, minNoticeMin, ignoreBookingId);
    }

    public async Task<BookingDto> CreateBookingAsync(Guid branchId, NewBookingRequest req, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var clientName = LimitedText(req.ClientName, MaxNameLength, "Nome do cliente");
        if (clientName.Length == 0) throw new AgendaValidationException("Informe o nome do cliente.");
        var vehicle = LimitedText(req.Vehicle, MaxVehicleLength, "Veículo");
        var notes = LimitedText(req.Notes, MaxNotesLength, "Observações");
        var date = AgendaFormat.ParseDate(req.Date);
        if (!VehicleTypes.Contains(req.VehicleType)) throw new AgendaValidationException("Tipo de veículo inválido.");
        var service = cfg.ServiceById(req.ServiceId);
        if (service is null || !service.Active) throw new AgendaValidationException("Serviço inválido.");
        var phone = string.IsNullOrWhiteSpace(req.Phone) ? string.Empty : AgendaFormat.NormalizePhone(req.Phone);
        if (phone.Length is > 0 and < 12 || phone.Length > MaxPhoneLength)
            throw new AgendaValidationException("WhatsApp inválido: informe DDD + número.");
        var origin = req.Origin == BookingOrigins.WhatsApp ? BookingOrigins.WhatsApp : BookingOrigins.Counter;
        var duration = service.DurationFor(req.VehicleType);
        var now = clock.NowLocal;

        return await repo.InDayTransactionAsync(branchId, date, async tx =>
        {
            // Revalida dentro da transação travada: a lista mostrada ao cliente pode estar velha.
            var slots = AvailabilityRules.AvailableStarts(
                cfg, date, service.Id, duration, Busy(await tx.DayBookingsAsync(date)), Busy(await tx.DayBlocksAsync(date)), now, 0, null);
            var slot = slots.FirstOrDefault(s => s.Start == req.Start) ?? throw new AgendaConflictException();
            var boxId = string.IsNullOrEmpty(req.BoxId) ? slot.BoxIds[0] : req.BoxId;
            if (!slot.BoxIds.Contains(boxId))
                throw new AgendaConflictException("Esse box acabou de ser ocupado nesse horário. Escolha outro box ou horário.");

            var utc = DateTime.UtcNow;
            var booking = new AgendaBooking
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                Date = date,
                StartMin = req.Start,
                EndMin = slot.End,
                BoxId = boxId,
                ServiceId = service.Id,
                ServiceName = service.Name,
                Price = service.PriceFor(req.VehicleType),
                VehicleType = req.VehicleType,
                Vehicle = vehicle,
                Plate = AgendaFormat.NormalizePlate(req.Plate),
                ClientName = clientName,
                Phone = phone,
                Notes = notes,
                Status = BookingStatuses.Scheduled,
                Origin = origin,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true,
            };
            await tx.InsertBookingAsync(booking);

            if (phone.Length > 0)
            {
                var variants = AgendaFormat.PhoneVariants(phone);
                var client = await tx.GetClientAsync(variants);
                if (client is null)
                {
                    client = new AgendaClient
                    {
                        Id = Guid.NewGuid(),
                        BranchId = branchId,
                        Phone = phone,
                        Name = booking.ClientName,
                        ReminderOptIn = req.ReminderOptIn,
                        OptInAt = req.ReminderOptIn ? now : null,
                        CreatedAt = utc,
                        UpdatedAt = utc,
                        Active = true,
                    };
                }
                else
                {
                    if (client.ReminderOptIn != req.ReminderOptIn) client.OptInAt = req.ReminderOptIn ? now : null;
                    client.ReminderOptIn = req.ReminderOptIn;
                    client.Name = booking.ClientName;
                    client.UpdatedAt = utc;
                }
                await tx.UpsertClientAsync(client);
                if (client.ReminderOptIn) await CreateReminderAsync(tx, cfg, booking, now);
            }

            return booking.ToDto();
        }, ct);
    }

    private static async Task CreateReminderAsync(IAgendaDayTransaction tx, AgendaConfigModel cfg, AgendaBooking booking, DateTime nowLocal)
    {
        var sendAt = ReminderRules.SendTime(booking.Date, booking.StartMin, nowLocal, cfg.Reminder);
        if (sendAt is null) return;
        var utc = DateTime.UtcNow;
        await tx.InsertReminderAsync(new AgendaReminder
        {
            Id = Guid.NewGuid(),
            BranchId = booking.BranchId,
            BookingId = booking.Id,
            ScheduledFor = sendAt.Value,
            Status = ReminderStatuses.Pending,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true,
        });
    }

    private static async Task CancelPendingRemindersAsync(IAgendaDayTransaction tx, Guid bookingId)
    {
        foreach (var r in await tx.RemindersOfBookingAsync(bookingId))
        {
            if (r.Status != ReminderStatuses.Pending) continue;
            r.Status = ReminderStatuses.Canceled;
            r.UpdatedAt = DateTime.UtcNow;
            await tx.UpdateReminderAsync(r);
        }
    }

    public async Task<BookingDto> SetStatusAsync(Guid branchId, Guid id, string status, CancellationToken ct = default)
    {
        if (!BookingStatuses.All.Contains(status)) throw new AgendaValidationException("Status inválido.");
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var now = clock.NowLocal;
        return await repo.InTransactionAsync(branchId, async tx =>
        {
            var b = await tx.GetBookingAsync(id) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
            var utc = DateTime.UtcNow;
            await tx.UpdateBookingStatusAsync(id, status, utc);
            b.Status = status;
            b.UpdatedAt = utc;
            if (!BookingStatuses.IsActive(status)) await CancelPendingRemindersAsync(tx, id);
            if (status == BookingStatuses.Scheduled && b.Phone.Length > 0)
            {
                var hasLive = (await tx.RemindersOfBookingAsync(id)).Any(r => r.Status != ReminderStatuses.Canceled);
                var client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(b.Phone));
                if (!hasLive && client is { ReminderOptIn: true }) await CreateReminderAsync(tx, cfg, b, now);
            }
            return b.ToDto();
        }, ct);
    }

    public async Task<BookingDto> RescheduleAsync(Guid branchId, Guid id, RescheduleRequest req, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var date = AgendaFormat.ParseDate(req.Date);
        var now = clock.NowLocal;
        return await repo.InDayTransactionAsync(branchId, date, async tx =>
        {
            var old = await tx.GetBookingAsync(id) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
            if (old.Status != BookingStatuses.Scheduled) throw new AgendaValidationException("Só dá para remarcar um agendamento ativo.");
            var service = cfg.ServiceById(old.ServiceId);
            if (service is null || !service.Active) throw new AgendaValidationException("O serviço deste agendamento não está mais ativo.");
            var duration = service.DurationFor(old.VehicleType);
            var slots = AvailabilityRules.AvailableStarts(
                cfg, date, service.Id, duration, Busy(await tx.DayBookingsAsync(date)), Busy(await tx.DayBlocksAsync(date)), now, 0, old.Id);
            var slot = slots.FirstOrDefault(s => s.Start == req.Start) ?? throw new AgendaConflictException();
            var boxId = string.IsNullOrEmpty(req.BoxId) ? slot.BoxIds[0] : req.BoxId;
            if (!slot.BoxIds.Contains(boxId))
                throw new AgendaConflictException("Esse box acabou de ser ocupado nesse horário. Escolha outro box ou horário.");

            var utc = DateTime.UtcNow;
            var created = new AgendaBooking
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                Date = date,
                StartMin = req.Start,
                EndMin = slot.End,
                BoxId = boxId,
                ServiceId = old.ServiceId,
                ServiceName = old.ServiceName,
                Price = old.Price,
                VehicleType = old.VehicleType,
                Vehicle = old.Vehicle,
                Plate = old.Plate,
                ClientName = old.ClientName,
                Phone = old.Phone,
                Notes = old.Notes,
                Status = BookingStatuses.Scheduled,
                Origin = old.Origin,
                RescheduledFrom = old.Id,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true,
            };
            // O horário antigo só é liberado junto com a reserva do novo (mesma transação).
            await tx.UpdateBookingStatusAsync(old.Id, BookingStatuses.Canceled, utc);
            await CancelPendingRemindersAsync(tx, old.Id);
            await tx.InsertBookingAsync(created);
            if (created.Phone.Length > 0)
            {
                var client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(created.Phone));
                if (client is { ReminderOptIn: true }) await CreateReminderAsync(tx, cfg, created, now);
            }
            return created.ToDto();
        }, ct);
    }

    public async Task DeleteBookingAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        if (!await repo.DeleteBookingAsync(branchId, id, ct)) throw new AgendaNotFoundException("Agendamento não encontrado.");
    }

    public Task<int> PruneAsync(Guid branchId, string before, CancellationToken ct = default) =>
        repo.PruneBookingsAsync(branchId, AgendaFormat.ParseDate(before), ct);

    /* ---------- Bloqueios ---------- */

    public async Task<IReadOnlyList<BlockDto>> ListBlocksAsync(Guid branchId, string from, string to, CancellationToken ct = default) =>
        (await repo.ListBlocksAsync(branchId, AgendaFormat.ParseDate(from), AgendaFormat.ParseDate(to), ct)).Select(k => k.ToDto()).ToList();

    public async Task<BlockDto> CreateBlockAsync(Guid branchId, NewBlockRequest req, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var date = AgendaFormat.ParseDate(req.Date);
        if (req.End <= req.Start || req.Start < 0 || req.End > 1440)
            throw new AgendaValidationException("O fim do bloqueio precisa ser depois do início.");
        var boxId = string.IsNullOrEmpty(req.BoxId) ? null : req.BoxId;
        if (boxId is not null && cfg.BoxById(boxId) is null) throw new AgendaValidationException("Box inválido.");
        var utc = DateTime.UtcNow;
        var block = new AgendaBlock
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            BoxId = boxId,
            Date = date,
            StartMin = req.Start,
            EndMin = req.End,
            Reason = LimitedText(req.Reason, MaxReasonLength, "Motivo"),
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true,
        };
        return (await repo.AddBlockAsync(block, ct)).ToDto();
    }

    public async Task DeleteBlockAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        if (!await repo.DeleteBlockAsync(branchId, id, ct)) throw new AgendaNotFoundException("Bloqueio não encontrado.");
    }

    /* ---------- Lembretes ---------- */

    public async Task<IReadOnlyList<ReminderDto>> ListRemindersAsync(Guid branchId, string from, string to, CancellationToken ct = default) =>
        (await repo.ListRemindersAsync(branchId, AgendaFormat.ParseDate(from), AgendaFormat.ParseDate(to), ct)).Select(r => r.ToDto()).ToList();

    public Task<IReadOnlyList<Guid>> BranchesWithDueRemindersAsync(CancellationToken ct = default) =>
        repo.ListBranchesWithDueRemindersAsync(clock.NowLocal, ct);

    public async Task<IReadOnlyList<ReminderDispatchDto>> DispatchRemindersAsync(Guid branchId, DispatchRequest req, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var now = clock.NowLocal;
        var until = string.IsNullOrWhiteSpace(req.Ate) ? now : AgendaFormat.ParseLocalDateTime(req.Ate);
        IReadOnlyList<Guid>? onlyIds = req.Ids?.Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();

        return await repo.InTransactionAsync(branchId, async tx =>
        {
            var output = new List<ReminderDispatchDto>();
            foreach (var r in await tx.PendingRemindersAsync(until, onlyIds))
            {
                var b = await tx.GetBookingAsync(r.BookingId);
                var client = b is null || b.Phone.Length == 0 ? null : await tx.GetClientAsync(AgendaFormat.PhoneVariants(b.Phone));
                var startsAt = b is null ? DateTime.MinValue : b.Date.AddMinutes(b.StartMin);
                // Mesmo filtro do worker: agendamento ativo, no futuro, cliente ainda quer lembrete.
                if (b is null || b.Status != BookingStatuses.Scheduled || startsAt <= now || client is not { ReminderOptIn: true })
                {
                    r.Status = ReminderStatuses.Canceled;
                    r.UpdatedAt = DateTime.UtcNow;
                    await tx.UpdateReminderAsync(r);
                    continue;
                }
                if (r.Attempts >= MaxReminderAttempts) continue;

                r.Attempts += 1;
                try
                {
                    r.WamId = await sender.SendAsync(b, cfg, ct);
                    r.Status = ReminderStatuses.Sent;
                    r.Error = null;
                }
                catch (Exception ex)
                {
                    r.Error = Truncate(ex.Message, MaxErrorLength);
                    r.Status = r.Attempts >= MaxReminderAttempts ? ReminderStatuses.Failed : ReminderStatuses.Pending;
                }
                r.UpdatedAt = DateTime.UtcNow;
                await tx.UpdateReminderAsync(r);
                output.Add(new ReminderDispatchDto(r.ToDto(), b.ToDto()));
            }
            return (IReadOnlyList<ReminderDispatchDto>)output;
        }, ct);
    }

    public async Task MarkDeliveryAsync(Guid branchId, DeliveryRequest req, CancellationToken ct = default)
    {
        if (req.Status is not (ReminderStatuses.Delivered or ReminderStatuses.Read or ReminderStatuses.Failed))
            throw new AgendaValidationException("Status de entrega inválido.");
        await repo.InTransactionAsync(branchId, async tx =>
        {
            var r = await tx.ReminderByWamIdAsync(req.WamId);
            if (r is null) return 0;
            var next = ReminderStatuses.Advance(r.Status, req.Status);
            if (next == r.Status) return 0;
            r.Status = next;
            if (next == ReminderStatuses.Failed) r.Error = Truncate(req.Erro ?? "Falha na entrega", MaxErrorLength);
            r.UpdatedAt = DateTime.UtcNow;
            await tx.UpdateReminderAsync(r);
            return 1;
        }, ct);
    }

    public async Task<ReminderDto?> RegisterAnswerAsync(Guid branchId, Guid bookingId, AnswerRequest req, CancellationToken ct = default)
    {
        if (!ReminderAnswers.IsValid(req.Resposta)) throw new AgendaValidationException("Resposta inválida.");
        var at = string.IsNullOrWhiteSpace(req.Em) ? clock.NowLocal : AgendaFormat.ParseLocalDateTime(req.Em);
        return await repo.InTransactionAsync(branchId, async tx =>
        {
            if (await tx.GetBookingAsync(bookingId) is null) throw new AgendaNotFoundException("Agendamento não encontrado.");
            var r = (await tx.RemindersOfBookingAsync(bookingId))
                .Where(x => x.Status is not (ReminderStatuses.Canceled or ReminderStatuses.Pending))
                .OrderByDescending(x => x.ScheduledFor)
                .FirstOrDefault();
            if (r is null) return null;
            r.Answer = req.Resposta;
            r.AnsweredAt = at;
            r.Status = ReminderStatuses.Advance(r.Status, ReminderStatuses.Read);
            r.UpdatedAt = DateTime.UtcNow;
            await tx.UpdateReminderAsync(r);
            return r.ToDto();
        }, ct);
    }

    /* ---------- Clientes ---------- */

    public async Task<ClientDto?> GetClientAsync(Guid branchId, string phone, CancellationToken ct = default) =>
        (await repo.GetClientAsync(branchId, AgendaFormat.PhoneVariants(phone), ct))?.ToDto();

    public async Task<IReadOnlyList<ClientDto>> ListClientsAsync(Guid branchId, CancellationToken ct = default) =>
        (await repo.ListClientsAsync(branchId, ct)).Select(c => c.ToDto()).ToList();

    public async Task<ClientDto> UpsertClientAsync(Guid branchId, string phone, ClientRequest req, CancellationToken ct = default)
    {
        var normalized = AgendaFormat.NormalizePhone(phone);
        if (normalized.Length is < 12 or > MaxPhoneLength) throw new AgendaValidationException("Telefone inválido.");
        var name = LimitedText(req.Name, MaxNameLength, "Nome do cliente");
        var now = clock.NowLocal;
        return await repo.InTransactionAsync(branchId, async tx =>
        {
            var variants = AgendaFormat.PhoneVariants(normalized);
            var utc = DateTime.UtcNow;
            var c = await tx.GetClientAsync(variants);
            if (c is null)
            {
                c = new AgendaClient
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    Phone = normalized,
                    Name = name,
                    ReminderOptIn = req.ReminderOptIn,
                    OptInAt = req.ReminderOptIn ? now : null,
                    CreatedAt = utc,
                    UpdatedAt = utc,
                    Active = true,
                };
            }
            else
            {
                if (c.ReminderOptIn != req.ReminderOptIn) c.OptInAt = req.ReminderOptIn ? now : null;
                c.ReminderOptIn = req.ReminderOptIn;
                if (name.Length > 0) c.Name = name;
                c.UpdatedAt = utc;
            }
            await tx.UpsertClientAsync(c);
            if (!c.ReminderOptIn) await tx.CancelPendingRemindersOfPhonesAsync(variants);
            return c.ToDto();
        }, ct);
    }
}
