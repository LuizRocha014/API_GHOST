using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Abstractions;
using Domain.Agenda;

namespace Application.Agenda;

public interface IAgendaService
{
    Task<IReadOnlyList<AgendaBranchDto>> BranchesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Histórico do agendamento (e dos que ele substituiu nas remarcações).</summary>
    Task<IReadOnlyList<AuditEntryDto>> HistoryAsync(Guid branchId, Guid id, CancellationToken ct = default);

    /// <summary>Avaliação do cliente (1 a 5) num atendimento concluído.</summary>
    Task<BookingDto> RateAsync(Guid branchId, Guid id, int rating, CancellationToken ct = default);

    /// <summary>Agendamento recorrente: cria um por data e devolve o que deu e o que não deu.</summary>
    Task<SeriesResultDto> CreateSeriesAsync(Guid branchId, SeriesRequest request, CancellationToken ct = default);

    /// <summary>Cancela este e os próximos da série que ainda estão marcados.</summary>
    Task<int> CancelSeriesAsync(Guid branchId, Guid id, CancellationToken ct = default);

    /// <summary>Registra (ou corrige) o recebimento: forma, desconto e valor.</summary>
    Task<BookingDto> PayAsync(Guid branchId, Guid id, PaymentRequest request, CancellationToken ct = default);
    Task<BookingDto> UndoPaymentAsync(Guid branchId, Guid id, CancellationToken ct = default);

    Task<string> GetConfigAsync(Guid branchId, CancellationToken ct = default);
    Task<string> SaveConfigAsync(Guid branchId, string json, CancellationToken ct = default);

    Task<IReadOnlyList<BookingDto>> ListBookingsAsync(Guid branchId, string from, string to, CancellationToken ct = default);
    Task<BookingDto> GetBookingAsync(Guid branchId, Guid id, CancellationToken ct = default);
    /// <summary>Horários livres para o serviço (+ adicionais em <paramref name="extraIds"/>) no tipo de veículo.</summary>
    Task<IReadOnlyList<SlotOptionDto>> AvailabilityAsync(
        Guid branchId, string date, string serviceId, string vehicleType, Guid? ignoreBookingId, int minNoticeMin,
        IReadOnlyList<string>? extraIds = null, CancellationToken ct = default);
    Task<BookingDto> CreateBookingAsync(Guid branchId, NewBookingRequest request, CancellationToken ct = default);

    /// <summary>Troca serviço, adicionais, veículo, preço ou observações de um agendamento marcado ou em atendimento.</summary>
    Task<BookingDto> UpdateBookingAsync(Guid branchId, Guid id, EditBookingRequest request, CancellationToken ct = default);
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

    Task<ClientDto> GetClientByIdAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<ClientDto> CreateClientAsync(Guid branchId, ClientFormRequest request, CancellationToken ct = default);
    Task<ClientDto> UpdateClientAsync(Guid branchId, Guid id, ClientFormRequest request, CancellationToken ct = default);
    Task DeleteClientAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> ClientBookingsAsync(Guid branchId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<LoyaltyPlanDto>> ListPlansAsync(Guid branchId, CancellationToken ct = default);
    Task<LoyaltyPlanDto> CreatePlanAsync(Guid branchId, LoyaltyPlanRequest request, CancellationToken ct = default);
    Task<LoyaltyPlanDto> UpdatePlanAsync(Guid branchId, Guid id, LoyaltyPlanRequest request, CancellationToken ct = default);
    Task DeletePlanAsync(Guid branchId, Guid id, CancellationToken ct = default);

    /// <summary>Planos do cliente com o uso no mês de <paramref name="date"/> (nulo = mês atual).</summary>
    Task<IReadOnlyList<ClientPlanDto>> ListClientPlansAsync(Guid branchId, Guid clientId, string? date, CancellationToken ct = default);

    /// <summary>
    /// Plano do cliente que cobriria o serviço no dia (o do carro, ou o sem carro do mesmo tipo) e quanto já foi usado no mês.
    /// Nulo quando nenhum plano em andamento inclui o serviço. Só consulta: quem aplica o plano é a reserva.
    /// </summary>
    Task<PlanCoverageDto?> PlanCoverageAsync(
        Guid branchId, Guid clientId, Guid? vehicleId, string vehicleType, string serviceId, string date, CancellationToken ct = default);
    Task<ClientPlanDto> CreateClientPlanAsync(Guid branchId, Guid clientId, ClientPlanRequest request, CancellationToken ct = default);
    Task<ClientPlanDto> UpdateClientPlanAsync(Guid branchId, Guid clientId, Guid id, ClientPlanRequest request, CancellationToken ct = default);

    /// <summary>Encerra o plano hoje: os atendimentos de hoje ainda contam nele.</summary>
    Task<ClientPlanDto> EndClientPlanAsync(Guid branchId, Guid clientId, Guid id, CancellationToken ct = default);

    /// <summary>Marca a mensalidade do mês ('AAAA-MM') como paga, com o valor atual do plano. 409 se já está paga.</summary>
    Task<ClientPlanDto> PayChargeAsync(Guid branchId, Guid clientId, Guid id, string month, CancellationToken ct = default);

    /// <summary>Desfaz o pagamento da mensalidade do mês (volta a pendente).</summary>
    Task<ClientPlanDto> UndoChargeAsync(Guid branchId, Guid clientId, Guid id, string month, CancellationToken ct = default);
}

public sealed class AgendaService(IAgendaRepository repo, IAgendaClock clock, IReminderSender sender, AgendaActor actor) : IAgendaService
{
    /* ---------- Histórico ---------- */

    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    private static string Money(decimal? v) => v is null ? "sem preço" : v.Value.ToString("C", PtBr);

    private static string When(DateTime date, int start) => $"{date:dd/MM} às {AgendaFormat.Hm(start)}";

    private static string StatusLabel(string status) => status switch
    {
        BookingStatuses.Scheduled => "Agendado",
        BookingStatuses.InProgress => "Em atendimento",
        BookingStatuses.Done => "Concluído",
        BookingStatuses.Canceled => "Cancelado",
        BookingStatuses.NoShow => "Não compareceu",
        _ => status,
    };

    private AgendaAuditEntry Audit(AgendaBooking b, string action, string details) => new()
    {
        Id = Guid.NewGuid(),
        BranchId = b.BranchId,
        BookingId = b.Id,
        ClientId = b.ClientId,
        Actor = Truncate(actor.Name, 120),
        Action = action,
        Details = Truncate(details, 1000),
        CreatedAt = DateTime.UtcNow,
    };

    public async Task<IReadOnlyList<AuditEntryDto>> HistoryAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        // O agendamento e os que ele substituiu (remarcações), para a história ficar inteira.
        var ids = new List<Guid> { id };
        var current = await repo.GetBookingAsync(branchId, id, ct);
        for (var i = 0; i < 20 && current?.RescheduledFrom is { } prev; i++)
        {
            ids.Add(prev);
            current = await repo.GetBookingAsync(branchId, prev, ct);
        }
        var entries = await repo.ListAuditAsync(branchId, ids, ct);
        return entries
            .OrderBy(e => e.CreatedAt)
            .Select(e => new AuditEntryDto(AgendaFormat.LocalDateTime(clock.ToLocal(e.CreatedAt)), e.Actor, e.Action, e.Details, e.BookingId?.ToString()))
            .ToList();
    }

    /* ---------- Recorrente ---------- */

    public const int MaxSeries = 52;

    public async Task<SeriesResultDto> CreateSeriesAsync(Guid branchId, SeriesRequest req, CancellationToken ct = default)
    {
        if (req.Count is < 2 or > MaxSeries) throw new AgendaValidationException($"Repita de 2 a {MaxSeries} vezes.");
        if (req.Every is not ("week" or "2weeks" or "month")) throw new AgendaValidationException("Repetição inválida: toda semana, a cada 2 semanas ou todo mês.");
        var first = AgendaFormat.ParseDate(req.Booking.Date);
        var seriesId = Guid.NewGuid();
        var items = new List<SeriesItemDto>();
        for (var i = 0; i < req.Count; i++)
        {
            var date = req.Every switch
            {
                "month" => first.AddMonths(i),
                "2weeks" => first.AddDays(14 * i),
                _ => first.AddDays(7 * i),
            };
            try
            {
                BookingDto created;
                try
                {
                    created = await CreateBookingAsync(branchId, req.Booking.CopyFor(date, seriesId, req.Booking.BoxId), ct);
                }
                catch (AgendaConflictException) when (!string.IsNullOrEmpty(req.Booking.BoxId))
                {
                    // O box de sempre está ocupado nesse dia: tenta outro box no mesmo horário.
                    created = await CreateBookingAsync(branchId, req.Booking.CopyFor(date, seriesId, null), ct);
                }
                items.Add(new SeriesItemDto(AgendaFormat.Date(date), created, null));
            }
            catch (AgendaConflictException)
            {
                items.Add(new SeriesItemDto(AgendaFormat.Date(date), null, "Horário ocupado ou fora do funcionamento nesse dia."));
            }
            catch (AgendaValidationException ex)
            {
                items.Add(new SeriesItemDto(AgendaFormat.Date(date), null, ex.Message));
            }
        }
        if (items.All(x => x.Booking is null))
            throw new AgendaConflictException("Nenhuma data da repetição está livre nesse horário. Escolha outro horário.");
        return new SeriesResultDto(seriesId.ToString(), items);
    }

    /// <summary>Cancela este agendamento e os próximos da mesma série que ainda estão marcados. Devolve quantos.</summary>
    public async Task<int> CancelSeriesAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var b = await repo.GetBookingAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
        if (b.SeriesId is not { } seriesId) throw new AgendaValidationException("Este agendamento não é de uma série.");
        var next = (await repo.ListSeriesAsync(branchId, seriesId, ct))
            .Where(x => x.Status == BookingStatuses.Scheduled && (x.Date > b.Date || (x.Date == b.Date && x.StartMin >= b.StartMin)))
            .ToList();
        foreach (var x in next) await SetStatusAsync(branchId, x.Id, BookingStatuses.Canceled, ct);
        return next.Count;
    }

    /* ---------- Avaliação ---------- */

    public static string RatingLabel(int rating) => rating >= 5 ? "Ótimo" : rating >= 3 ? "Bom" : "Ruim";

    public async Task<BookingDto> RateAsync(Guid branchId, Guid id, int rating, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5) throw new AgendaValidationException("Nota de 1 a 5.");
        return await repo.InTransactionAsync(branchId, async tx =>
        {
            var b = await tx.GetBookingAsync(id) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
            if (b.Status != BookingStatuses.Done) throw new AgendaValidationException("Só dá para avaliar um atendimento concluído.");
            b.Rating = rating;
            b.RatedAt = DateTime.UtcNow;
            b.UpdatedAt = b.RatedAt.Value;
            await tx.UpdateBookingRatingAsync(b);
            await tx.InsertAuditAsync(Audit(b, "rating", $"Cliente avaliou o atendimento: {RatingLabel(rating)}"));
            return b.ToDto();
        }, ct);
    }

    /* ---------- Pagamento ---------- */

    public async Task<BookingDto> PayAsync(Guid branchId, Guid id, PaymentRequest req, CancellationToken ct = default)
    {
        if (!PaymentMethods.All.Contains(req.Method)) throw new AgendaValidationException("Escolha a forma de pagamento.");
        return await repo.InTransactionAsync(branchId, async tx =>
        {
            var b = await tx.GetBookingAsync(id) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
            if (b.Status == BookingStatuses.Canceled) throw new AgendaValidationException("Agendamento cancelado não recebe pagamento.");
            var price = b.Price ?? 0m;
            var discount = Math.Round(req.Discount ?? 0m, 2, MidpointRounding.AwayFromZero);
            if (discount < 0 || discount > price) throw new AgendaValidationException("O desconto precisa ficar entre zero e o preço do atendimento.");
            var amount = Math.Round(req.Amount ?? price - discount, 2, MidpointRounding.AwayFromZero);
            if (amount is < 0 or > 1_000_000) throw new AgendaValidationException("Valor recebido inválido.");
            var correction = b.PaidAt is not null;
            var utc = DateTime.UtcNow;
            b.Discount = discount == 0 ? null : discount;
            b.PaymentMethod = req.Method;
            b.PaidAmount = amount;
            b.PaidAt = utc;
            b.UpdatedAt = utc;
            await tx.UpdateBookingPaymentAsync(b);
            await tx.InsertAuditAsync(Audit(b, "paid",
                $"{(correction ? "Pagamento corrigido: " : "Recebido ")}{Money(amount)} em {PaymentMethods.Label(req.Method)}" +
                (discount > 0 ? $" (desconto de {Money(discount)})" : string.Empty)));
            return b.ToDto();
        }, ct);
    }

    public async Task<BookingDto> UndoPaymentAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await repo.InTransactionAsync(branchId, async tx =>
        {
            var b = await tx.GetBookingAsync(id) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
            if (b.PaidAt is null) throw new AgendaValidationException("Esse atendimento não tem pagamento registrado.");
            var was = $"{Money(b.PaidAmount)} em {PaymentMethods.Label(b.PaymentMethod)}";
            b.Discount = null;
            b.PaymentMethod = null;
            b.PaidAmount = null;
            b.PaidAt = null;
            b.UpdatedAt = DateTime.UtcNow;
            await tx.UpdateBookingPaymentAsync(b);
            await tx.InsertAuditAsync(Audit(b, "payment_undone", $"Pagamento desfeito (era {was})"));
            return b.ToDto();
        }, ct);

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
    public const int MaxColorLength = 40;
    public const int MaxVehiclesPerClient = 50;
    public const int ClientHistoryLimit = 200;

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
        Guid branchId, string date, string serviceId, string vehicleType, Guid? ignoreBookingId, int minNoticeMin,
        IReadOnlyList<string>? extraIds = null, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var day = AgendaFormat.ParseDate(date);
        var service = cfg.ServiceById(serviceId);
        if (service is null || !service.Active) return [];
        // Na remarcação os adicionais vão como foram marcados (igual ao RescheduleAsync), mesmo os que saíram do catálogo.
        var rescheduled = ignoreBookingId is { } ignore ? await repo.GetBookingAsync(branchId, ignore, ct) : null;
        var extras = rescheduled is not null
            ? BookingExtras.Parse(rescheduled.ExtrasJson)
            : BookingExtras.Resolve(cfg, service, vehicleType, extraIds);
        var bookings = await repo.ListBookingsAsync(branchId, day, day, ct);
        var blocks = await repo.ListBlocksAsync(branchId, day, day, ct);
        return AvailabilityRules.AvailableStarts(
            cfg, day, service.Id, service.DurationFor(vehicleType) + BookingExtras.Duration(extras), Busy(bookings), Busy(blocks),
            clock.NowLocal, minNoticeMin, ignoreBookingId, BookingExtras.ServiceIds(extras));
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
        var extras = BookingExtras.Resolve(cfg, service, req.VehicleType, req.ExtraIds);
        var duration = service.DurationFor(req.VehicleType) + BookingExtras.Duration(extras);
        var now = clock.NowLocal;
        var clientId = OptionalId(req.ClientId, "Cliente");
        var vehicleId = OptionalId(req.VehicleId, "Carro");
        if (vehicleId is not null && clientId is null) throw new AgendaValidationException("Escolha o cliente do carro.");

        return await repo.InDayTransactionAsync(branchId, date, async tx =>
        {
            // Revalida dentro da transação travada: a lista mostrada ao cliente pode estar velha.
            var slots = AvailabilityRules.AvailableStarts(
                cfg, date, service.Id, duration, Busy(await tx.DayBookingsAsync(date)), Busy(await tx.DayBlocksAsync(date)), now, 0, null,
                BookingExtras.ServiceIds(extras));
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
                Price = BookingExtras.Total(service.PriceFor(req.VehicleType), extras),
                ExtrasJson = BookingExtras.Json(extras),
                VehicleType = req.VehicleType,
                Vehicle = vehicle,
                Plate = AgendaFormat.NormalizePlate(req.Plate),
                ClientName = clientName,
                Phone = phone,
                Notes = notes,
                Status = BookingStatuses.Scheduled,
                Origin = origin,
                SeriesId = req.SeriesId,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true,
            };

            AgendaClient? client = null;
            if (clientId is { } cid)
            {
                client = await tx.GetClientByIdAsync(cid) ?? throw new AgendaValidationException("Cliente não encontrado.");
                if (client.Phone.Length == 0 && phone.Length > 0 && await PhoneOwnerAsync(tx, phone, client.Id) is null) client.Phone = phone;
            }
            else if (phone.Length > 0)
            {
                client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(phone))
                         ?? new AgendaClient { Id = Guid.NewGuid(), BranchId = branchId, Phone = phone, CreatedAt = utc, Active = true };
            }

            if (client is not null)
            {
                if (client.ReminderOptIn != req.ReminderOptIn) client.OptInAt = req.ReminderOptIn ? now : null;
                client.ReminderOptIn = req.ReminderOptIn;
                client.Name = booking.ClientName;
                client.UpdatedAt = utc;
                await tx.UpsertClientAsync(client);
                booking.ClientId = client.Id;
                booking.VehicleId = await ResolveVehicleAsync(tx, client, vehicleId, booking, utc);
                if (req.UsePlan) await ApplyPlanAsync(tx, booking, null, cfg);
            }

            await tx.InsertBookingAsync(booking);
            await tx.InsertAuditAsync(Audit(booking, "created",
                $"Agendado {(origin == BookingOrigins.WhatsApp ? "pelo WhatsApp" : "no balcão")} para {When(date, booking.StartMin)} · " +
                $"{BookingExtras.Title(booking.ServiceName, extras)} · " +
                (booking.ClientPlanId is not null ? "pelo plano de fidelidade" : Money(booking.Price))));
            if (phone.Length > 0 && client is { ReminderOptIn: true })
            {
                await CreateReminderAsync(tx, cfg, booking, now);
                // Pelo bot o cliente já recebe a confirmação na conversa; no balcão, manda o resumo pelo WhatsApp.
                if (origin == BookingOrigins.Counter && cfg.Notify.Booked) await CreateNoticeAsync(tx, booking, ReminderTypes.Booked, now);
            }
            return booking.ToDto();
        }, ct);
    }

    /// <summary>Mensagem para sair já (confirmação do balcão, carro pronto): entra na mesma fila do lembrete.</summary>
    private static Task CreateNoticeAsync(IAgendaDayTransaction tx, AgendaBooking booking, string type, DateTime nowLocal)
    {
        var utc = DateTime.UtcNow;
        return tx.InsertReminderAsync(new AgendaReminder
        {
            Id = Guid.NewGuid(),
            BranchId = booking.BranchId,
            BookingId = booking.Id,
            Type = type,
            ScheduledFor = nowLocal,
            Status = ReminderStatuses.Pending,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true,
        });
    }

    private static Guid? OptionalId(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null
        : Guid.TryParse(value, out var id) ? id
        : throw new AgendaValidationException($"{field} inválido.");

    /// <summary>Outro cliente ativo com este WhatsApp (com ou sem o nono dígito), ou nulo.</summary>
    private static async Task<AgendaClient?> PhoneOwnerAsync(IAgendaDayTransaction tx, string phone, Guid? self)
    {
        if (phone.Length == 0) return null;
        var owner = await tx.GetClientAsync(AgendaFormat.PhoneVariants(phone));
        return owner is not null && owner.Id != self ? owner : null;
    }

    /// <summary>
    /// Carro do agendamento entre os carros do cliente: o escolhido, ou o da mesma placa (sem placa, o do mesmo modelo).
    /// Carro que ainda não existe entra no cadastro do cliente.
    /// </summary>
    private static async Task<Guid?> ResolveVehicleAsync(IAgendaDayTransaction tx, AgendaClient client, Guid? vehicleId, AgendaBooking b, DateTime utc)
    {
        var vehicles = await tx.ClientVehiclesAsync(client.Id);
        if (vehicleId is { } vid)
            return vehicles.Any(v => v.Id == vid) ? vid : throw new AgendaValidationException("Esse carro não é deste cliente.");
        if (b.Plate.Length == 0 && b.Vehicle.Length == 0) return null;

        var found = b.Plate.Length > 0
            ? vehicles.FirstOrDefault(v => v.Plate == b.Plate)
            : vehicles.FirstOrDefault(v => v.Plate.Length == 0 && string.Equals(v.Vehicle, b.Vehicle, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
        {
            if (found.Vehicle.Length == 0 && b.Vehicle.Length > 0)
            {
                found.Vehicle = b.Vehicle;
                found.UpdatedAt = utc;
                await tx.UpsertVehicleAsync(found);
            }
            return found.Id;
        }

        var created = new AgendaClientVehicle
        {
            Id = Guid.NewGuid(),
            BranchId = client.BranchId,
            ClientId = client.Id,
            Vehicle = b.Vehicle,
            Plate = b.Plate,
            VehicleType = b.VehicleType,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true,
        };
        await tx.UpsertVehicleAsync(created);
        return created.Id;
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
        // Segundo lembrete: só sai se o cliente não tiver respondido o primeiro (conferido no envio).
        if (ReminderRules.RetryTime(booking.Date, booking.StartMin, sendAt.Value, cfg.Reminder) is { } retryAt)
            await CreateNoticeAsync(tx, booking, ReminderTypes.ConfirmationRetry, retryAt);
    }

    private static async Task CancelPendingRemindersAsync(IAgendaDayTransaction tx, Guid bookingId, IReadOnlyList<string>? types = null)
    {
        foreach (var r in await tx.RemindersOfBookingAsync(bookingId))
        {
            if (r.Status != ReminderStatuses.Pending || (types is not null && !types.Contains(r.Type))) continue;
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
            var previous = b.Status;
            var utc = DateTime.UtcNow;
            await tx.UpdateBookingStatusAsync(id, status, utc);
            b.Status = status;
            b.UpdatedAt = utc;
            if (previous != status) await tx.InsertAuditAsync(Audit(b, "status", $"{StatusLabel(previous)} → {StatusLabel(status)}"));
            if (!BookingStatuses.IsActive(status)) await CancelPendingRemindersAsync(tx, id);
            // Atendimento começou ou terminou: lembrete e confirmação que ainda não saíram perdem o sentido.
            else if (status is BookingStatuses.InProgress or BookingStatuses.Done)
                await CancelPendingRemindersAsync(tx, id, [ReminderTypes.ConfirmationD1, ReminderTypes.ConfirmationRetry, ReminderTypes.Booked]);
            if (status == BookingStatuses.Scheduled && b.Phone.Length > 0)
            {
                var hasLive = (await tx.RemindersOfBookingAsync(id)).Any(r => r.Type == ReminderTypes.ConfirmationD1 && r.Status != ReminderStatuses.Canceled);
                var client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(b.Phone));
                if (!hasLive && client is { ReminderOptIn: true }) await CreateReminderAsync(tx, cfg, b, now);
            }
            if (status == BookingStatuses.Done && previous != BookingStatuses.Done && cfg.Notify.Ready && b.Phone.Length > 0)
            {
                // "Seu carro está pronto": uma vez por agendamento, só para quem aceitou mensagens.
                var sent = (await tx.RemindersOfBookingAsync(id)).Any(r => r.Type == ReminderTypes.Ready);
                var client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(b.Phone));
                if (!sent && client is { ReminderOptIn: true }) await CreateNoticeAsync(tx, b, ReminderTypes.Ready, now);
            }
            if (status == BookingStatuses.Done && previous != BookingStatuses.Done && cfg.Notify.Review && b.Phone.Length > 0)
            {
                // "Como foi o atendimento?": algumas horas depois; à noite, fica para as 10h do dia seguinte.
                var asked = (await tx.RemindersOfBookingAsync(id)).Any(r => r.Type == ReminderTypes.Review);
                var client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(b.Phone));
                if (!asked && client is { ReminderOptIn: true })
                {
                    var at = now.AddHours(Math.Clamp(cfg.Notify.ReviewDelayHours, 0, 48));
                    if (at.Hour >= 21) at = at.Date.AddDays(1).AddHours(10);
                    else if (at.Hour < 8) at = at.Date.AddHours(10);
                    await CreateNoticeAsync(tx, b, ReminderTypes.Review, at);
                }
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
            // Os adicionais vão junto, com o tempo e o preço de quando foram marcados.
            var extras = BookingExtras.Parse(old.ExtrasJson);
            var duration = service.DurationFor(old.VehicleType) + BookingExtras.Duration(extras);
            var slots = AvailabilityRules.AvailableStarts(
                cfg, date, service.Id, duration, Busy(await tx.DayBookingsAsync(date)), Busy(await tx.DayBlocksAsync(date)), now, 0, old.Id,
                BookingExtras.ServiceIds(extras));
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
                ExtrasJson = old.ExtrasJson,
                VehicleType = old.VehicleType,
                Vehicle = old.Vehicle,
                Plate = old.Plate,
                ClientName = old.ClientName,
                Phone = old.Phone,
                Notes = old.Notes,
                Status = BookingStatuses.Scheduled,
                Origin = old.Origin,
                RescheduledFrom = old.Id,
                ClientId = old.ClientId,
                VehicleId = old.VehicleId,
                // Pagamento adiantado vai junto.
                Discount = old.Discount,
                PaymentMethod = old.PaymentMethod,
                PaidAmount = old.PaidAmount,
                PaidAt = old.PaidAt,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true,
            };
            // O horário antigo só é liberado junto com a reserva do novo (mesma transação).
            await tx.UpdateBookingStatusAsync(old.Id, BookingStatuses.Canceled, utc);
            await CancelPendingRemindersAsync(tx, old.Id);
            if (old.ClientPlanId is { } planId)
            {
                // A vez do mês antigo foi devolvida com o cancelamento; no dia novo o plano precisa valer e ter vez sobrando.
                created.ClientPlanId = null;
                created.Price = BookingExtras.Total(service.PriceFor(old.VehicleType), extras);
                await ApplyPlanAsync(tx, created, planId, cfg);
            }
            await tx.InsertBookingAsync(created);
            await tx.InsertAuditAsync(Audit(old, "rescheduled", $"Remarcado para {When(date, req.Start)}"));
            await tx.InsertAuditAsync(Audit(created, "rescheduled", $"Remarcado de {When(old.Date, old.StartMin)} para {When(date, req.Start)}"));
            if (created.Phone.Length > 0)
            {
                var client = await tx.GetClientAsync(AgendaFormat.PhoneVariants(created.Phone));
                if (client is { ReminderOptIn: true }) await CreateReminderAsync(tx, cfg, created, now);
            }
            return created.ToDto();
        }, ct);
    }

    public async Task<BookingDto> UpdateBookingAsync(Guid branchId, Guid id, EditBookingRequest req, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var current = await repo.GetBookingAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
        var notes = req.Notes is null ? null : LimitedText(req.Notes, MaxNotesLength, "Observações");
        var vehicleText = req.Vehicle is null ? null : LimitedText(req.Vehicle, MaxVehicleLength, "Veículo");
        if (req.VehicleType is not null && !VehicleTypes.Contains(req.VehicleType)) throw new AgendaValidationException("Tipo de veículo inválido.");
        if (req.Price is < 0 or > 1_000_000) throw new AgendaValidationException("Preço inválido.");
        var vehicleId = OptionalId(req.VehicleId, "Carro");

        return await repo.InDayTransactionAsync(branchId, current.Date, async tx =>
        {
            var b = await tx.GetBookingAsync(id) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
            if (b.Status is not (BookingStatuses.Scheduled or BookingStatuses.InProgress))
                throw new AgendaValidationException("Só dá para editar um agendamento marcado ou em atendimento.");
            var utc = DateTime.UtcNow;
            var oldType = b.VehicleType;
            var before = (Title: BookingExtras.Title(b.ServiceName, BookingExtras.Parse(b.ExtrasJson)), b.Price, b.Vehicle, b.Plate, b.VehicleType, b.Notes, b.EndMin);
            var client = b.ClientId is { } cid ? await tx.GetClientByIdAsync(cid) : null;

            if (vehicleId is { } vid)
            {
                if (client is null) throw new AgendaValidationException("Este agendamento não tem cliente cadastrado para escolher o carro.");
                var car = (await tx.ClientVehiclesAsync(client.Id)).FirstOrDefault(v => v.Id == vid)
                          ?? throw new AgendaValidationException("Esse carro não é deste cliente.");
                b.VehicleId = car.Id;
                b.Vehicle = car.Vehicle;
                b.Plate = car.Plate;
                b.VehicleType = car.VehicleType;
            }
            else
            {
                if (req.VehicleType is not null) b.VehicleType = req.VehicleType;
                if (vehicleText is not null || req.Plate is not null)
                {
                    if (vehicleText is not null) b.Vehicle = vehicleText;
                    if (req.Plate is not null) b.Plate = AgendaFormat.NormalizePlate(req.Plate);
                    // Placa nova entra nos carros do cliente, como no agendamento.
                    if (client is not null) b.VehicleId = await ResolveVehicleAsync(tx, client, null, b, utc);
                }
            }
            if (notes is not null) b.Notes = notes;

            var serviceChanged = req.ServiceId is not null && req.ServiceId != b.ServiceId;
            var typeChanged = b.VehicleType != oldType;
            if (serviceChanged || typeChanged || req.ExtraIds is not null)
            {
                var service = cfg.ServiceById(serviceChanged ? req.ServiceId : b.ServiceId);
                if (service is null || !service.Active) throw new AgendaValidationException("Serviço inválido ou inativo.");
                // Sem lista nova, os adicionais continuam: os do catálogo com o preço e o tempo do tipo de veículo atual,
                // os que saíram dele como foram marcados.
                var current = BookingExtras.Parse(b.ExtrasJson);
                var extras = req.ExtraIds is not null
                    ? BookingExtras.Resolve(cfg, service, b.VehicleType, req.ExtraIds, current: current)
                    : BookingExtras.Resolve(cfg, service, b.VehicleType, current.Select(x => x.Id), skipMissing: true, current: current);
                var duration = service.DurationFor(b.VehicleType) + BookingExtras.Duration(extras);
                var box = cfg.BoxById(b.BoxId);
                var serves = box is not null && box.Serves(service.Id) && BookingExtras.ServiceIds(extras).All(box.Serves);
                if (!serves || !AvailabilityRules.Fits(
                        cfg, b.Date, b.BoxId, b.StartMin, duration, Busy(await tx.DayBookingsAsync(b.Date)), Busy(await tx.DayBlocksAsync(b.Date)), b.Id))
                {
                    throw new AgendaConflictException(
                        $"Com essa mudança o atendimento passa a durar {AgendaFormat.Hm(duration)} h e não cabe mais nesse horário e " +
                        $"{(box?.Name ?? "box")}. Remarque para um horário livre e depois edite.");
                }
                b.ServiceId = service.Id;
                b.ServiceName = service.Name;
                b.ExtrasJson = BookingExtras.Json(extras);
                b.EndMin = b.StartMin + duration;
                b.Price = BookingExtras.Total(service.PriceFor(b.VehicleType), extras);
                // O plano é refeito: pode passar a cobrir (ou deixar de cobrir) o novo serviço ou carro.
                b.ClientPlanId = null;
                if (client is not null) await ApplyPlanAsync(tx, b, null, cfg);
            }
            if (req.Price is { } manual) b.Price = Math.Round(manual, 2, MidpointRounding.AwayFromZero);
            // Desconto dado antes não passa do preço novo.
            if (b.Discount is { } d && d > (b.Price ?? 0m))
            {
                b.Discount = b.Price is > 0 ? b.Price : null;
                await tx.UpdateBookingPaymentAsync(b);
            }
            b.UpdatedAt = utc;
            await tx.UpdateBookingAsync(b);

            var changes = new List<string>();
            var title = BookingExtras.Title(b.ServiceName, BookingExtras.Parse(b.ExtrasJson));
            if (title != before.Title) changes.Add($"serviço: {before.Title} → {title}");
            if (b.EndMin != before.EndMin) changes.Add($"fim: {AgendaFormat.Hm(before.EndMin)} → {AgendaFormat.Hm(b.EndMin)}");
            if (b.Price != before.Price) changes.Add($"preço: {Money(before.Price)} → {Money(b.Price)}");
            if (b.VehicleType != before.VehicleType) changes.Add($"tipo: {before.VehicleType} → {b.VehicleType}");
            if (b.Vehicle != before.Vehicle) changes.Add($"veículo: {(before.Vehicle.Length == 0 ? "—" : before.Vehicle)} → {(b.Vehicle.Length == 0 ? "—" : b.Vehicle)}");
            if (b.Plate != before.Plate) changes.Add($"placa: {(before.Plate.Length == 0 ? "—" : before.Plate)} → {(b.Plate.Length == 0 ? "—" : b.Plate)}");
            if (b.Notes != before.Notes) changes.Add("observações");
            if (changes.Count > 0) await tx.InsertAuditAsync(Audit(b, "edited", "Editado: " + string.Join("; ", changes)));
            return b.ToDto();
        }, ct);
    }

    public async Task DeleteBookingAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var b = await repo.GetBookingAsync(branchId, id, ct);
        if (!await repo.DeleteBookingAsync(branchId, id, ct)) throw new AgendaNotFoundException("Agendamento não encontrado.");
        if (b is not null) await repo.InsertAuditAsync(Audit(b, "deleted", "Registro excluído"), ct);
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
                // Cada mensagem só sai se ainda faz sentido: lembrete e confirmação para agendamento marcado e no futuro;
                // "carro pronto" para atendimento concluído. E o cliente precisa continuar aceitando mensagens.
                var stillValid = b is not null && client is { ReminderOptIn: true } && r.Type switch
                {
                    ReminderTypes.Ready => b.Status == BookingStatuses.Done,
                    ReminderTypes.Review => b.Status == BookingStatuses.Done && b.Rating is null,
                    ReminderTypes.ConfirmationRetry => b.Status == BookingStatuses.Scheduled && startsAt > now
                        && !(await tx.RemindersOfBookingAsync(b.Id)).Any(x => x.Answer is not null),
                    _ => b.Status == BookingStatuses.Scheduled && startsAt > now,
                };
                if (!stillValid)
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
                    r.WamId = await sender.SendAsync(r.Type, b!, cfg, ct);
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
                output.Add(new ReminderDispatchDto(r.ToDto(), b!.ToDto()));
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
            var booking = await tx.GetBookingAsync(bookingId) ?? throw new AgendaNotFoundException("Agendamento não encontrado.");
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
            var answer = req.Resposta switch
            {
                ReminderAnswers.Confirmed => "confirmou",
                ReminderAnswers.Canceled => "pediu para cancelar",
                _ => "pediu para remarcar",
            };
            await tx.InsertAuditAsync(Audit(booking, "answer", $"Cliente {answer} pelo lembrete"));
            return r.ToDto();
        }, ct);
    }

    /* ---------- Clientes ---------- */

    /// <summary>Por cliente: soma das mensalidades pagas e quantos meses de plano estão em aberto.</summary>
    private async Task<Dictionary<Guid, (decimal Paid, int Pending)>> PlanMoneyAsync(Guid branchId, Guid? clientId, CancellationToken ct)
    {
        var plans = await repo.ListBranchClientPlansAsync(branchId, clientId, ct);
        var payments = await repo.ListPlanPaymentsAsync(branchId, clientId, ct);
        var today = clock.NowLocal.Date;
        var byPlan = payments.ToLookup(p => p.ClientPlanId);
        return plans.GroupBy(p => p.ClientId).ToDictionary(
            g => g.Key,
            g => (g.Sum(p => byPlan[p.Id].Sum(x => x.Amount)),
                  g.Sum(p => LoyaltyRules.Charges(p, byPlan[p.Id], today).Count(c => c.Status == "pending"))));
    }

    private async Task<ClientDto> ClientDtoAsync(Guid branchId, AgendaClient c, CancellationToken ct)
    {
        var vehicles = await repo.ListClientVehiclesAsync(branchId, c.Id, ct);
        var stats = await repo.ClientStatsAsync(branchId, clock.NowLocal.Date, c.Id, ct);
        var plans = await repo.ClientPlanNamesAsync(branchId, c.Id, ct);
        var money = (await PlanMoneyAsync(branchId, c.Id, ct)).GetValueOrDefault(c.Id);
        return c.ToDto(vehicles, stats.FirstOrDefault(), plans.Select(p => p.Name).ToList(), money.Paid, money.Pending);
    }

    private async Task<ClientDto> ClientDtoAsync(Guid branchId, Guid id, CancellationToken ct) =>
        await ClientDtoAsync(branchId, await repo.GetClientByIdAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Cliente não encontrado."), ct);

    public async Task<ClientDto?> GetClientAsync(Guid branchId, string phone, CancellationToken ct = default)
    {
        var c = await repo.GetClientAsync(branchId, AgendaFormat.PhoneVariants(phone), ct);
        return c is null ? null : await ClientDtoAsync(branchId, c, ct);
    }

    public async Task<IReadOnlyList<ClientDto>> ListClientsAsync(Guid branchId, CancellationToken ct = default)
    {
        var clients = await repo.ListClientsAsync(branchId, ct);
        var vehicles = (await repo.ListClientVehiclesAsync(branchId, null, ct)).ToLookup(v => v.ClientId);
        var stats = (await repo.ClientStatsAsync(branchId, clock.NowLocal.Date, null, ct)).ToDictionary(s => s.ClientId);
        var plans = (await repo.ClientPlanNamesAsync(branchId, null, ct)).ToLookup(p => p.ClientId, p => p.Name);
        var money = await PlanMoneyAsync(branchId, null, ct);
        return clients.Select(c =>
        {
            var m = money.GetValueOrDefault(c.Id);
            return c.ToDto(vehicles[c.Id].ToList(), stats.GetValueOrDefault(c.Id), plans[c.Id].ToList(), m.Paid, m.Pending);
        }).ToList();
    }

    public Task<ClientDto> GetClientByIdAsync(Guid branchId, Guid id, CancellationToken ct = default) => ClientDtoAsync(branchId, id, ct);

    public async Task<IReadOnlyList<BookingDto>> ClientBookingsAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        _ = await repo.GetClientByIdAsync(branchId, id, ct) ?? throw new AgendaNotFoundException("Cliente não encontrado.");
        return (await repo.ListClientBookingsAsync(branchId, id, ClientHistoryLimit, ct)).Select(b => b.ToDto()).ToList();
    }

    private sealed record VehicleForm(Guid? Id, string Vehicle, string Plate, string VehicleType, string Color);

    private sealed record ClientForm(
        string Name, string Phone, bool ReminderOptIn, bool Vip, string Notes, bool BotBlocked, IReadOnlyList<VehicleForm> Vehicles);

    private static ClientForm ValidateClientForm(ClientFormRequest req)
    {
        var name = LimitedText(req.Name, MaxNameLength, "Nome do cliente");
        if (name.Length == 0) throw new AgendaValidationException("Informe o nome do cliente.");
        var phone = string.IsNullOrWhiteSpace(req.Phone) ? string.Empty : AgendaFormat.NormalizePhone(req.Phone);
        if (phone.Length is > 0 and < 12 || phone.Length > MaxPhoneLength)
            throw new AgendaValidationException("WhatsApp inválido: informe DDD + número.");
        var notes = LimitedText(req.Notes, MaxNotesLength, "Observações");

        var vehicles = new List<VehicleForm>();
        foreach (var v in req.Vehicles ?? [])
        {
            var model = LimitedText(v.Vehicle, MaxVehicleLength, "Carro");
            var plate = AgendaFormat.NormalizePlate(v.Plate);
            if (model.Length == 0 && plate.Length == 0) throw new AgendaValidationException("Informe o modelo ou a placa de cada carro.");
            var type = v.VehicleType ?? string.Empty;
            if (!VehicleTypes.Contains(type)) throw new AgendaValidationException("Tipo de veículo inválido.");
            var color = LimitedText(v.Color, MaxColorLength, "Cor");
            vehicles.Add(new VehicleForm(OptionalId(v.Id, "Carro"), model, plate, type, color));
        }
        if (vehicles.Count > MaxVehiclesPerClient)
            throw new AgendaValidationException($"Um cliente pode ter no máximo {MaxVehiclesPerClient} carros.");
        var repeated = vehicles.Where(v => v.Plate.Length > 0).GroupBy(v => v.Plate).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null) throw new AgendaValidationException($"A placa {repeated.Key} aparece em mais de um carro.");

        return new ClientForm(name, phone, req.ReminderOptIn && phone.Length > 0, req.Vip, notes, req.BotBlocked, vehicles);
    }

    private static void ApplyForm(AgendaClient c, ClientForm form, DateTime nowLocal, DateTime utc)
    {
        if (c.ReminderOptIn != form.ReminderOptIn) c.OptInAt = form.ReminderOptIn ? nowLocal : null;
        c.Name = form.Name;
        c.Phone = form.Phone;
        c.ReminderOptIn = form.ReminderOptIn;
        c.Vip = form.Vip;
        c.Notes = form.Notes;
        c.BotBlocked = form.BotBlocked;
        c.UpdatedAt = utc;
    }

    private static async Task SaveVehiclesAsync(IAgendaDayTransaction tx, AgendaClient c, IReadOnlyList<VehicleForm> form, DateTime utc)
    {
        var existing = (await tx.ClientVehiclesAsync(c.Id)).ToDictionary(v => v.Id);
        var keep = new List<Guid>();
        foreach (var f in form)
        {
            AgendaClientVehicle v;
            if (f.Id is { } id)
            {
                v = existing.GetValueOrDefault(id) ?? throw new AgendaValidationException("Carro não encontrado neste cliente.");
            }
            else
            {
                v = new AgendaClientVehicle { Id = Guid.NewGuid(), BranchId = c.BranchId, ClientId = c.Id, CreatedAt = utc.AddMilliseconds(10 * keep.Count), Active = true };
            }
            v.Vehicle = f.Vehicle;
            v.Plate = f.Plate;
            v.VehicleType = f.VehicleType;
            v.Color = f.Color;
            v.UpdatedAt = utc;
            await tx.UpsertVehicleAsync(v);
            keep.Add(v.Id);
        }
        await tx.DeactivateVehiclesAsync(c.Id, keep, utc);
    }

    public async Task<ClientDto> CreateClientAsync(Guid branchId, ClientFormRequest req, CancellationToken ct = default)
    {
        var form = ValidateClientForm(req);
        var now = clock.NowLocal;
        var id = await repo.InTransactionAsync(branchId, async tx =>
        {
            var owner = await PhoneOwnerAsync(tx, form.Phone, null);
            if (owner is not null) throw new AgendaConflictException($"Esse WhatsApp já é do cliente {owner.Name}.");
            var utc = DateTime.UtcNow;
            var c = new AgendaClient { Id = Guid.NewGuid(), BranchId = branchId, CreatedAt = utc, Active = true };
            ApplyForm(c, form, now, utc);
            await tx.UpsertClientAsync(c);
            await SaveVehiclesAsync(tx, c, form.Vehicles, utc);
            return c.Id;
        }, ct);
        return await ClientDtoAsync(branchId, id, ct);
    }

    public async Task<ClientDto> UpdateClientAsync(Guid branchId, Guid id, ClientFormRequest req, CancellationToken ct = default)
    {
        var form = ValidateClientForm(req);
        var now = clock.NowLocal;
        await repo.InTransactionAsync(branchId, async tx =>
        {
            var c = await tx.GetClientByIdAsync(id) ?? throw new AgendaNotFoundException("Cliente não encontrado.");
            var owner = await PhoneOwnerAsync(tx, form.Phone, c.Id);
            if (owner is not null) throw new AgendaConflictException($"Esse WhatsApp já é do cliente {owner.Name}.");
            var oldPhone = c.Phone;
            var hadOptIn = c.ReminderOptIn;
            var utc = DateTime.UtcNow;
            ApplyForm(c, form, now, utc);
            await tx.UpsertClientAsync(c);
            await SaveVehiclesAsync(tx, c, form.Vehicles, utc);
            if (hadOptIn && oldPhone.Length > 0 && (!c.ReminderOptIn || c.Phone != oldPhone))
                await tx.CancelPendingRemindersOfPhonesAsync(AgendaFormat.PhoneVariants(oldPhone));
            return true;
        }, ct);
        return await ClientDtoAsync(branchId, id, ct);
    }

    public async Task DeleteClientAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        await repo.InTransactionAsync(branchId, async tx =>
        {
            var c = await tx.GetClientByIdAsync(id) ?? throw new AgendaNotFoundException("Cliente não encontrado.");
            await tx.DeactivateClientAsync(c.Id, DateTime.UtcNow);
            if (c.Phone.Length > 0) await tx.CancelPendingRemindersOfPhonesAsync(AgendaFormat.PhoneVariants(c.Phone));
            return true;
        }, ct);
    }

    public async Task<ClientDto> UpsertClientAsync(Guid branchId, string phone, ClientRequest req, CancellationToken ct = default)
    {
        var normalized = AgendaFormat.NormalizePhone(phone);
        if (normalized.Length is < 12 or > MaxPhoneLength) throw new AgendaValidationException("Telefone inválido.");
        var name = LimitedText(req.Name, MaxNameLength, "Nome do cliente");
        var now = clock.NowLocal;
        var saved = await repo.InTransactionAsync(branchId, async tx =>
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
            return c;
        }, ct);
        return await ClientDtoAsync(branchId, saved, ct);
    }

    /* ---------- Planos de fidelidade ---------- */

    private sealed record PlanForm(string Name, decimal DiscountPercent, IReadOnlyList<PlanItemModel> Items);

    /// <summary>Nome, desconto de 0 a 100% e pelo menos um serviço ativo, sem repetir, de 1 a 31 vezes por mês.</summary>
    private static PlanForm ValidatePlanForm(AgendaConfigModel cfg, string? rawName, decimal discount, List<PlanItemRequest>? rawItems)
    {
        var name = LimitedText(rawName, MaxNameLength, "Nome do plano");
        if (name.Length == 0) throw new AgendaValidationException("Informe o nome do plano.");
        if (discount is < 0 or > 100) throw new AgendaValidationException("O desconto precisa ficar entre 0% e 100%.");
        var items = new List<PlanItemModel>();
        foreach (var raw in rawItems ?? [])
        {
            var service = cfg.ServiceById(raw.ServiceId) ?? throw new AgendaValidationException("Serviço do plano não encontrado.");
            if (!service.Active) throw new AgendaValidationException($"O serviço {service.Name} está desativado: tire do plano ou ative em Ajustes.");
            if (items.Any(i => i.ServiceId == service.Id)) throw new AgendaValidationException($"O serviço {service.Name} aparece duas vezes no plano.");
            if (raw.TimesPerMonth is < 1 or > LoyaltyRules.MaxTimesPerMonth)
                throw new AgendaValidationException($"{service.Name}: informe de 1 a {LoyaltyRules.MaxTimesPerMonth} vezes por mês.");
            items.Add(new PlanItemModel(service.Id, raw.TimesPerMonth));
        }
        if (items.Count == 0) throw new AgendaValidationException("Escolha pelo menos um serviço para o plano.");
        if (items.Count > LoyaltyRules.MaxItems) throw new AgendaValidationException($"Um plano pode ter no máximo {LoyaltyRules.MaxItems} serviços.");
        return new PlanForm(name, Math.Round(discount, 2, MidpointRounding.AwayFromZero), items);
    }

    private static string ServiceName(AgendaConfigModel cfg, string serviceId) => cfg.ServiceById(serviceId)?.Name ?? "Serviço removido";

    private static LoyaltyPlanDto PlanDto(AgendaConfigModel cfg, AgendaLoyaltyPlan p, int activeClients)
    {
        var items = LoyaltyRules.ParseItems(p.ItemsJson);
        var prices = new List<PlanPriceDto>();
        foreach (var type in VehicleTypes)
        {
            var full = LoyaltyRules.FullPrice(cfg, items, type);
            if (full is not null) prices.Add(new PlanPriceDto(type, full.Value, LoyaltyRules.ApplyDiscount(full.Value, p.DiscountPercent)));
        }
        return new LoyaltyPlanDto(
            p.Id.ToString(),
            p.Name,
            p.Description,
            p.DiscountPercent,
            items.Select(i => new PlanItemDto(i.ServiceId, ServiceName(cfg, i.ServiceId), i.TimesPerMonth)).ToList(),
            prices,
            activeClients);
    }

    public async Task<IReadOnlyList<LoyaltyPlanDto>> ListPlansAsync(Guid branchId, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var plans = await repo.ListLoyaltyPlansAsync(branchId, ct);
        var clients = (await repo.PlanSubscribersAsync(branchId, ct)).ToDictionary(r => r.PlanId, r => r.Clients);
        return plans.Select(p => PlanDto(cfg, p, clients.GetValueOrDefault(p.Id))).ToList();
    }

    private async Task<LoyaltyPlanDto> SavePlanAsync(Guid branchId, Guid? id, LoyaltyPlanRequest req, CancellationToken ct)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var form = ValidatePlanForm(cfg, req.Name, req.DiscountPercent, req.Items);
        var description = LimitedText(req.Description, MaxNotesLength, "Descrição");
        var utc = DateTime.UtcNow;
        var plan = id is { } existingId
            ? await repo.GetLoyaltyPlanAsync(branchId, existingId, ct) ?? throw new AgendaNotFoundException("Plano não encontrado.")
            : new AgendaLoyaltyPlan { Id = Guid.NewGuid(), BranchId = branchId, CreatedAt = utc, Active = true };
        plan.Name = form.Name;
        plan.Description = description;
        plan.DiscountPercent = form.DiscountPercent;
        plan.ItemsJson = LoyaltyRules.ItemsJson(form.Items);
        plan.UpdatedAt = utc;
        await repo.SaveLoyaltyPlanAsync(plan, ct);
        var clients = (await repo.PlanSubscribersAsync(branchId, ct)).FirstOrDefault(r => r.PlanId == plan.Id)?.Clients ?? 0;
        return PlanDto(cfg, plan, clients);
    }

    public Task<LoyaltyPlanDto> CreatePlanAsync(Guid branchId, LoyaltyPlanRequest request, CancellationToken ct = default) =>
        SavePlanAsync(branchId, null, request, ct);

    /// <summary>Muda só o modelo: quem já aderiu continua com o plano como estava (nome, serviços e preço fechados na adesão).</summary>
    public Task<LoyaltyPlanDto> UpdatePlanAsync(Guid branchId, Guid id, LoyaltyPlanRequest request, CancellationToken ct = default) =>
        SavePlanAsync(branchId, id, request, ct);

    /// <summary>Tira o plano da lista. Os clientes que aderiram continuam com ele até encerrar.</summary>
    public async Task DeletePlanAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        if (!await repo.DeactivateLoyaltyPlanAsync(branchId, id, ct)) throw new AgendaNotFoundException("Plano não encontrado.");
    }

    private static ClientPlanDto ClientPlanDto(
        AgendaConfigModel cfg, AgendaClientPlan p, DateTime month, IEnumerable<PlanUsageRow> usage,
        IEnumerable<AgendaPlanPayment> payments, IEnumerable<PlanUpcomingRow> upcoming, DateTime today)
    {
        var used = usage.Where(u => u.ClientPlanId == p.Id).ToDictionary(u => u.ServiceId, u => u.Used);
        var charges = LoyaltyRules.Charges(p, payments, today, cfg.PlanBilling.GraceDays);
        return new ClientPlanDto(
            p.Id.ToString(),
            p.ClientId.ToString(),
            p.PlanId?.ToString(),
            p.Name,
            p.DiscountPercent,
            p.VehicleId?.ToString(),
            p.VehicleType,
            p.FullPrice,
            p.Price,
            AgendaFormat.Date(p.StartDate),
            p.EndDate is null ? null : AgendaFormat.Date(p.EndDate.Value),
            LoyaltyRules.MonthKey(month),
            LoyaltyRules.ParseItems(p.ItemsJson)
                .Select(i => new PlanUsageDto(i.ServiceId, ServiceName(cfg, i.ServiceId), i.TimesPerMonth, used.GetValueOrDefault(i.ServiceId)))
                .ToList(),
            charges,
            upcoming.FirstOrDefault(u => u.ClientPlanId == p.Id)?.Bookings ?? 0,
            LoyaltyRules.DueDayOf(p),
            cfg.PlanBilling.SuspendOverdue && p.EndDate is null && charges.Any(c => c.Overdue));
    }

    public async Task<IReadOnlyList<ClientPlanDto>> ListClientPlansAsync(Guid branchId, Guid clientId, string? date, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        _ = await repo.GetClientByIdAsync(branchId, clientId, ct) ?? throw new AgendaNotFoundException("Cliente não encontrado.");
        var day = string.IsNullOrWhiteSpace(date) ? clock.NowLocal.Date : AgendaFormat.ParseDate(date);
        var (from, to) = LoyaltyRules.Month(day);
        var plans = await repo.ListClientPlansAsync(branchId, clientId, ct);
        var ids = plans.Select(p => p.Id).ToList();
        var usage = await repo.PlanUsageAsync(branchId, ids, from, to, ct);
        var payments = await repo.ListPlanPaymentsAsync(branchId, clientId, ct);
        var today = clock.NowLocal.Date;
        var upcoming = await repo.PlanUpcomingAsync(branchId, ids, today, ct);
        return plans.Select(p => ClientPlanDto(cfg, p, from, usage, payments, upcoming, today)).ToList();
    }

    public async Task<PlanCoverageDto?> PlanCoverageAsync(
        Guid branchId, Guid clientId, Guid? vehicleId, string vehicleType, string serviceId, string date, CancellationToken ct = default)
    {
        var day = AgendaFormat.ParseDate(date);
        var plan = LoyaltyRules.PlanFor(await repo.ListClientPlansAsync(branchId, clientId, ct), vehicleId, vehicleType, day);
        if (plan is null) return null;
        var item = LoyaltyRules.ParseItems(plan.ItemsJson).FirstOrDefault(i => i.ServiceId == serviceId);
        if (item is null) return null;
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        if (cfg.PlanBilling.SuspendOverdue &&
            LoyaltyRules.IsOverdue(plan, await repo.ListPlanPaymentsAsync(branchId, clientId, ct), clock.NowLocal.Date, cfg.PlanBilling.GraceDays))
            return null;
        var (from, to) = LoyaltyRules.Month(day);
        var used = (await repo.PlanUsageAsync(branchId, [plan.Id], from, to, ct))
            .Where(u => u.ClientPlanId == plan.Id && u.ServiceId == serviceId)
            .Sum(u => u.Used);
        return new PlanCoverageDto(plan.Id.ToString(), plan.Name, item.TimesPerMonth, used, LoyaltyRules.MonthKey(day));
    }

    private async Task<ClientPlanDto> ClientPlanDtoAsync(Guid branchId, AgendaConfigModel cfg, AgendaClientPlan plan, CancellationToken ct)
    {
        var today = clock.NowLocal.Date;
        var (from, to) = LoyaltyRules.Month(today);
        var usage = await repo.PlanUsageAsync(branchId, [plan.Id], from, to, ct);
        var payments = await repo.ListPlanPaymentsAsync(branchId, plan.ClientId, ct);
        var upcoming = await repo.PlanUpcomingAsync(branchId, [plan.Id], today, ct);
        return ClientPlanDto(cfg, plan, from, usage, payments, upcoming, today);
    }

    /// <summary>
    /// Grava a adesão: valida o plano, acha o carro, fecha o preço para o tipo de veículo e recusa
    /// um segundo plano em andamento para o mesmo carro (ou, sem carro, para o mesmo tipo de veículo).
    /// </summary>
    private async Task<ClientPlanDto> SaveClientPlanAsync(Guid branchId, Guid clientId, Guid? id, ClientPlanRequest req, CancellationToken ct)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var form = ValidatePlanForm(cfg, req.Name, req.DiscountPercent, req.Items);
        var planId = OptionalId(req.PlanId, "Plano");
        var vehicleId = OptionalId(req.VehicleId, "Carro");
        if (req.DueDay is { } dd && dd is < 1 or > 28) throw new AgendaValidationException("Dia do vencimento: de 1 a 28.");
        if (planId is { } pid && await repo.GetLoyaltyPlanAsync(branchId, pid, ct) is null && id is null)
            throw new AgendaValidationException("Plano não encontrado. Ele pode ter sido excluído; escolha outro.");
        var today = clock.NowLocal.Date;

        var saved = await repo.InTransactionAsync(branchId, async tx =>
        {
            var client = await tx.GetClientByIdAsync(clientId) ?? throw new AgendaNotFoundException("Cliente não encontrado.");
            string vehicleType;
            string carLabel;
            if (vehicleId is { } vid)
            {
                var car = (await tx.ClientVehiclesAsync(client.Id)).FirstOrDefault(v => v.Id == vid)
                          ?? throw new AgendaValidationException("Esse carro não é deste cliente.");
                vehicleType = car.VehicleType;
                carLabel = string.Join(" · ", new[] { car.Vehicle, car.Plate }.Where(x => x.Length > 0));
            }
            else
            {
                vehicleType = req.VehicleType ?? string.Empty;
                if (!VehicleTypes.Contains(vehicleType)) throw new AgendaValidationException("Escolha o carro ou o tipo de veículo do plano.");
                carLabel = vehicleType;
            }

            var full = LoyaltyRules.FullPrice(cfg, form.Items, vehicleType);
            if (full is null)
            {
                var missing = form.Items.Select(i => cfg.ServiceById(i.ServiceId)!).First(s => s.PriceFor(vehicleType) is null);
                throw new AgendaValidationException($"O serviço {missing.Name} não tem preço para {vehicleType}: defina o preço em Ajustes ou tire do plano.");
            }

            var plans = await tx.ClientPlansAsync(client.Id);
            var plan = id is { } existingId
                ? plans.FirstOrDefault(p => p.Id == existingId) ?? throw new AgendaNotFoundException("Plano do cliente não encontrado.")
                : null;
            if (plan?.EndDate is not null) throw new AgendaValidationException("Este plano já foi encerrado.");

            var clash = plans.FirstOrDefault(p => p.EndDate is null && p.Id != plan?.Id &&
                (vehicleId is not null ? p.VehicleId == vehicleId : p.VehicleId is null && p.VehicleType == vehicleType));
            if (clash is not null)
                throw new AgendaConflictException($"{carLabel} já tem o plano {clash.Name}. Edite ou encerre esse plano antes.");

            var utc = DateTime.UtcNow;
            plan ??= new AgendaClientPlan
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                ClientId = client.Id,
                StartDate = today,
                CreatedAt = utc,
                Active = true,
            };
            plan.PlanId = planId ?? plan.PlanId;
            plan.Name = form.Name;
            plan.DiscountPercent = form.DiscountPercent;
            plan.ItemsJson = LoyaltyRules.ItemsJson(form.Items);
            plan.VehicleId = vehicleId;
            plan.VehicleType = vehicleType;
            plan.FullPrice = full.Value;
            plan.Price = LoyaltyRules.ApplyDiscount(full.Value, form.DiscountPercent);
            if (req.DueDay is { } due) plan.DueDay = due;
            else if (plan.DueDay == 0) plan.DueDay = Math.Min(plan.StartDate.Day, 28);
            plan.UpdatedAt = utc;
            await tx.UpsertClientPlanAsync(plan);
            return plan;
        }, ct);
        return await ClientPlanDtoAsync(branchId, cfg, saved, ct);
    }

    public Task<ClientPlanDto> CreateClientPlanAsync(Guid branchId, Guid clientId, ClientPlanRequest request, CancellationToken ct = default) =>
        SaveClientPlanAsync(branchId, clientId, null, request, ct);

    public Task<ClientPlanDto> UpdateClientPlanAsync(Guid branchId, Guid clientId, Guid id, ClientPlanRequest request, CancellationToken ct = default) =>
        SaveClientPlanAsync(branchId, clientId, id, request, ct);

    public async Task<ClientPlanDto> EndClientPlanAsync(Guid branchId, Guid clientId, Guid id, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var today = clock.NowLocal.Date;
        var plan = await repo.InTransactionAsync(branchId, async tx =>
        {
            var p = (await tx.ClientPlansAsync(clientId)).FirstOrDefault(x => x.Id == id)
                    ?? throw new AgendaNotFoundException("Plano do cliente não encontrado.");
            if (p.EndDate is not null) throw new AgendaValidationException("Este plano já foi encerrado.");
            // Encerrar no mesmo dia da adesão: o plano vale só hoje.
            p.EndDate = today < p.StartDate ? p.StartDate : today;
            p.UpdatedAt = DateTime.UtcNow;
            await tx.UpsertClientPlanAsync(p);
            // O que já estava marcado para depois do encerramento sai do plano e volta ao preço normal do serviço.
            foreach (var b in await tx.PlanBookingsAfterAsync(p.Id, p.EndDate.Value))
                await tx.ReleaseBookingFromPlanAsync(
                    b.Id,
                    cfg.ServiceById(b.ServiceId) is { } s ? BookingExtras.Total(s.PriceFor(b.VehicleType), BookingExtras.Parse(b.ExtrasJson)) : b.Price,
                    p.UpdatedAt);
            return p;
        }, ct);
        return await ClientPlanDtoAsync(branchId, cfg, plan, ct);
    }

    public async Task<ClientPlanDto> PayChargeAsync(Guid branchId, Guid clientId, Guid id, string month, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var m = LoyaltyRules.ParseMonth(month);
        var now = clock.NowLocal;
        var plan = await repo.InTransactionAsync(branchId, async tx =>
        {
            var p = (await tx.ClientPlansAsync(clientId)).FirstOrDefault(x => x.Id == id)
                    ?? throw new AgendaNotFoundException("Plano do cliente não encontrado.");
            if (!LoyaltyRules.ChargeMonths(p, now.Date).Contains(m))
                throw new AgendaValidationException($"O plano {p.Name} não tem mensalidade em {LoyaltyRules.MonthKey(m)}.");
            if ((await tx.PlanPaymentsAsync(p.Id)).Any(x => x.Month.Date == m))
                throw new AgendaConflictException($"A mensalidade de {LoyaltyRules.MonthKey(m)} já está paga.");
            var utc = DateTime.UtcNow;
            await tx.InsertPlanPaymentAsync(new AgendaPlanPayment
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                ClientId = p.ClientId,
                ClientPlanId = p.Id,
                Month = m,
                Amount = p.Price,
                PaidAt = now,
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true,
            });
            return p;
        }, ct);
        return await ClientPlanDtoAsync(branchId, cfg, plan, ct);
    }

    public async Task<ClientPlanDto> UndoChargeAsync(Guid branchId, Guid clientId, Guid id, string month, CancellationToken ct = default)
    {
        var (_, cfg) = await LoadConfigAsync(branchId, ct);
        var m = LoyaltyRules.ParseMonth(month);
        var plan = await repo.InTransactionAsync(branchId, async tx =>
        {
            var p = (await tx.ClientPlansAsync(clientId)).FirstOrDefault(x => x.Id == id)
                    ?? throw new AgendaNotFoundException("Plano do cliente não encontrado.");
            var paid = (await tx.PlanPaymentsAsync(p.Id)).FirstOrDefault(x => x.Month.Date == m)
                       ?? throw new AgendaNotFoundException($"A mensalidade de {LoyaltyRules.MonthKey(m)} não está paga.");
            await tx.DeactivatePlanPaymentAsync(paid.Id, DateTime.UtcNow);
            return p;
        }, ct);
        return await ClientPlanDtoAsync(branchId, cfg, plan, ct);
    }

    /// <summary>
    /// Cobre o agendamento pelo plano do cliente quando o plano vale no dia, inclui o serviço e ainda tem vez no mês
    /// (preço zero: o cliente paga a mensalidade). Com <paramref name="onlyPlanId"/>, só esse plano serve (remarcação).
    /// Sem plano que cubra, o agendamento fica como está (preço normal).
    /// </summary>
    private async Task ApplyPlanAsync(IAgendaDayTransaction tx, AgendaBooking booking, Guid? onlyPlanId, AgendaConfigModel cfg)
    {
        if (booking.ClientId is not { } clientId) return;
        var plans = await tx.ClientPlansAsync(clientId);
        var plan = onlyPlanId is { } pid
            ? plans.FirstOrDefault(p => p.Id == pid && LoyaltyRules.Covers(p, booking.Date))
            : LoyaltyRules.PlanFor(plans, booking.VehicleId, booking.VehicleType, booking.Date);
        if (plan is null) return;
        // Mensalidade atrasada suspende o plano (se a estética quiser): cobra normal até pagar.
        if (cfg.PlanBilling.SuspendOverdue &&
            LoyaltyRules.IsOverdue(plan, await tx.PlanPaymentsAsync(plan.Id), clock.NowLocal.Date, cfg.PlanBilling.GraceDays))
            return;
        var item = LoyaltyRules.ParseItems(plan.ItemsJson).FirstOrDefault(i => i.ServiceId == booking.ServiceId);
        if (item is null) return;

        await tx.LockPlanMonthAsync(plan.Id, booking.Date);
        var (from, to) = LoyaltyRules.Month(booking.Date);
        var used = await tx.PlanServiceUsageAsync(plan.Id, booking.ServiceId, from, to);
        if (used >= item.TimesPerMonth) return;

        // O plano cobre o serviço principal; os adicionais são cobrados à parte.
        booking.ClientPlanId = plan.Id;
        booking.Price = BookingExtras.Price(BookingExtras.Parse(booking.ExtrasJson));
    }
}
