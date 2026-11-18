using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Agenda;
using Microsoft.Extensions.Logging;

namespace Application.Bot;

/// <summary>
/// Agendamento pelo WhatsApp: máquina de estados determinística, sem LLM.
///
///     idle → choosingService → (choosingVehicle | choosingVehicleType) → choosingDay → choosingSlot
///          → askingVehicle → confirming → (reserva) → idle
///     myBookings → bookingAction → (remarcar: choosingDay… | cancelar)
///
/// Regras transversais: sessão expira depois de Bot.SessionTimeoutMin parada; entrada inválida repete a pergunta
/// e, depois de N tentativas, oferece atendente; comandos globais (menu, cancelar, atendente, meus agendamentos,
/// informações, parar/quero lembretes); mensagem repetida da Meta é ignorada pelo id; respostas do lembrete e de
/// "meus agendamentos" (conf/rem/canc/cask) valem em qualquer passo e conferem se o número é o dono do agendamento.
/// Proteções (Bot.MaxUpcoming, Bot.NoShowLimit, cliente bloqueado) valem para agendamento novo.
/// As regras de agenda e de plano são as do <see cref="IAgendaService"/>.
/// </summary>
public sealed partial class BookingFlow(IAgendaService agenda, IBotStore store, IAgendaClock clock, ILogger<BookingFlow> logger)
{
    private const int SlotsPerPage = 8;
    private const int MaxListedBookings = 9;
    private static readonly TimeSpan AttendantTimeout = TimeSpan.FromHours(12);

    private static readonly HashSet<string> CmdMenu = ["menu", "inicio", "comecar", "recomecar"];
    private static readonly HashSet<string> CmdCancel = ["cancelar", "sair", "parar agendamento"];
    private static readonly HashSet<string> CmdAttendant = ["atendente", "humano", "falar com atendente", "falar com alguem", "falar com a equipe"];
    private static readonly HashSet<string> CmdOptOut = ["parar lembretes", "nao quero lembretes", "parar lembrete"];
    private static readonly HashSet<string> CmdOptIn = ["quero lembretes", "quero lembrete", "voltar lembretes"];
    private static readonly HashSet<string> CmdMyBookings =
    [
        "meus agendamentos", "meus horarios", "meu agendamento", "meu horario", "agendamentos", "horarios marcados",
        "remarcar", "desmarcar", "cancelar horario", "cancelar agendamento", "cancelar meu horario", "cancelar meu agendamento",
        "remarcar horario", "remarcar agendamento", "remarcar meu horario",
    ];
    private static readonly HashSet<string> CmdInfo =
    [
        "informacoes", "informacao", "info", "endereco", "localizacao", "onde fica", "horario de funcionamento", "funcionamento",
        "precos", "preco", "valores", "tabela", "tabela de precos", "pagamento", "formas de pagamento",
    ];

    /// <summary>Primeira mensagem perguntando preço, endereço ou horário: responde com as informações antes do menu.</summary>
    private static readonly string[] InfoKeywords =
    [
        "quanto custa", "quanto fica", "quanto sai", "qual o valor", "qual valor", "preco", "valores", "endereco", "onde fica",
        "localizacao", "que horas abre", "que horas fecha", "horario de funcionamento", "abre hoje", "aberto hoje",
        "forma de pagamento", "formas de pagamento", "aceita pix", "aceita cartao",
    ];
    /// <summary>Saudação fora do fluxo de agendamento (vendo os horários marcados) volta ao menu.</summary>
    private static readonly HashSet<string> Greetings = ["oi", "oie", "ola", "opa", "bom dia", "boa tarde", "boa noite", "e ai", "eai"];
    private static readonly HashSet<string> Yes = ["sim", "ok", "confirmar", "confirmo", "pode", "pode confirmar", "s"];
    private static readonly HashSet<string> No = ["nao", "n", "voltar", "manter"];
    private static readonly HashSet<string> UnderstoodKinds = ["text", "interactive", "button"];

    private sealed class Ctx(Guid branchId, BotSession s, AgendaConfigModel cfg, DateTime now, IncomingMessage msg, IMessagingChannel channel, bool expired, CancellationToken ct)
    {
        public Guid BranchId { get; } = branchId;
        public BotSession S { get; } = s;
        public AgendaConfigModel Cfg { get; } = cfg;
        public DateTime Now { get; } = now;
        public IncomingMessage Msg { get; } = msg;
        public IMessagingChannel Channel { get; } = channel;
        public bool Expired { get; } = expired;
        public CancellationToken Ct { get; } = ct;
        public string Today => BotFormat.Iso(Now.Date);

        // Cache da mensagem: cliente e histórico são lidos uma vez por mensagem.
        public bool ClientLoaded { get; set; }
        public ClientDto? Client { get; set; }
        public IReadOnlyList<BookingDto>? History { get; set; }
    }

    /// <summary>Processa uma mensagem do cliente e responde pelo <paramref name="channel"/>.</summary>
    public async Task HandleAsync(Guid branchId, IncomingMessage msg, IMessagingChannel channel, CancellationToken ct = default)
    {
        if (!await store.TryRegisterMessageAsync(msg.Id, DateTime.UtcNow, ct))
        {
            logger.LogInformation("Bot: mensagem duplicada ignorada ({MessageId})", msg.Id);
            return;
        }

        var now = clock.NowLocal;
        await store.LogMessageAsync(branchId, msg.Phone, "in", msg.Kind, DescribeIncoming(msg), msg.Id, null, ct);
        var s = await store.GetSessionAsync(branchId, msg.Phone, ct) ?? new BotSession { Phone = msg.Phone, UpdatedAt = now };
        var before = s.State;
        try
        {
            var cfg = AgendaConfigModel.Parse(await agenda.GetConfigAsync(branchId, ct));
            if (!string.IsNullOrWhiteSpace(msg.ProfileName)) s.ProfileName = msg.ProfileName;
            var idle = now - s.UpdatedAt;
            var timedOut = s.State == BotStates.WithAttendant
                ? idle > AttendantTimeout
                : s.State != BotStates.Idle && idle > TimeSpan.FromMinutes(cfg.Bot.SessionTimeoutMin);
            if (timedOut) s.Reset();
            var ctx = new Ctx(branchId, s, cfg, now, msg, channel, timedOut && before != BotStates.WithAttendant, ct);
            if (!UnderstoodKinds.Contains(msg.Kind)) await OnUnsupportedAsync(ctx);
            else await RouteAsync(ctx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Bot: erro ao tratar a mensagem {MessageId} de {Phone}", msg.Id, msg.Phone);
            try
            {
                await SendAsync(branchId, msg.Phone, channel,
                    new TextMessage("Tive um problema para consultar a agenda agora 😕 Tente de novo em instantes ou digite *atendente*."), ct);
            }
            catch (Exception sendError)
            {
                logger.LogWarning(sendError, "Bot: não consegui avisar {Phone} do erro", msg.Phone);
            }
        }
        s.UpdatedAt = now;
        await store.SaveSessionAsync(branchId, s, ct);
        if (before != s.State) logger.LogInformation("Bot {Phone}: {From} → {To}", msg.Phone, before, s.State);
    }

    private static string DescribeIncoming(IncomingMessage m) =>
        m.Text ?? m.SelectionTitle ?? m.SelectionId ?? $"[{m.Kind}]";

    /* ---------- Roteamento ---------- */

    private async Task RouteAsync(Ctx ctx)
    {
        var s = ctx.S;
        var cmd = BotFormat.Normalize(ctx.Msg.Text);

        // Opção tocada ou digitada que vale em qualquer passo.
        var picked = ctx.Msg.SelectionId ?? (s.Options.Count > 0 ? Selected(ctx) : null);
        var reminder = picked is null ? null : ReminderRegex().Match(picked);
        if (reminder is { Success: true }) { await OnBookingReplyAsync(ctx, reminder.Groups[1].Value, reminder.Groups[2].Value); return; }

        if (picked == "nav:menu" || CmdMenu.Contains(cmd)) { s.Reset(); await AskServiceAsync(ctx); return; }
        if (picked == "nav:attendant" || CmdAttendant.Contains(cmd)) { await ToAttendantAsync(ctx); return; }
        if (picked == "nav:my" || CmdMyBookings.Contains(cmd)) { await ShowMyBookingsAsync(ctx); return; }
        if (picked == "nav:info" || CmdInfo.Contains(cmd)) { await ShowInfoAsync(ctx); return; }
        if (CmdCancel.Contains(cmd)) { await CancelFlowAsync(ctx); return; }
        if (CmdOptOut.Contains(cmd)) { await SetReminderOptInAsync(ctx, false); return; }
        if (CmdOptIn.Contains(cmd)) { await SetReminderOptInAsync(ctx, true); return; }

        // Serviço tocado numa lista antiga (o WhatsApp mantém as listas clicáveis): começa um agendamento com ele.
        if (ctx.Msg.SelectionId?.StartsWith("svc:", StringComparison.Ordinal) == true && s.State is not (BotStates.ChoosingService or BotStates.WithAttendant))
        {
            s.Reset();
            s.State = BotStates.ChoosingService;
            await OnServiceAsync(ctx);
            return;
        }
        if (Greetings.Contains(cmd) && s.State is BotStates.MyBookings or BotStates.BookingAction)
        {
            s.Reset();
            await AskServiceAsync(ctx);
            return;
        }

        switch (s.State)
        {
            case BotStates.WithAttendant: return; // a equipe responde pela caixa de entrada
            case BotStates.ChoosingService: await OnServiceAsync(ctx); return;
            case BotStates.ChoosingVehicle: await OnVehicleChoiceAsync(ctx); return;
            case BotStates.ChoosingVehicleType: await OnVehicleTypeAsync(ctx); return;
            case BotStates.ChoosingDay: await OnDayAsync(ctx); return;
            case BotStates.ChoosingSlot: await OnSlotAsync(ctx); return;
            case BotStates.AskingVehicle: await OnVehicleAsync(ctx); return;
            case BotStates.Confirming: await OnConfirmAsync(ctx); return;
            case BotStates.MyBookings: await OnMyBookingsAsync(ctx); return;
            case BotStates.BookingAction: await OnBookingActionAsync(ctx); return;
            case BotStates.ChoosingExtras: await OnExtrasAsync(ctx); return;
            default:
                if (InfoKeywords.Any(k => cmd.Contains(k, StringComparison.Ordinal)))
                {
                    await SendInfoAsync(ctx);
                    await AskServiceAsync(ctx, greet: false);
                    return;
                }
                await AskServiceAsync(ctx);
                return;
        }
    }

    /// <summary>Opção escolhida: tocada na lista, ou digitada (número da opção, nome ou horário).</summary>
    private static string? Selected(Ctx ctx)
    {
        var msg = ctx.Msg;
        var s = ctx.S;
        if (msg.SelectionId is not null) return msg.SelectionId;
        var t = BotFormat.Normalize(msg.Text);
        if (t.Length == 0 || s.Options.Count == 0) return null;
        if (int.TryParse(t, out var n) && n >= 1 && n <= s.Options.Count) return s.Options[n - 1].Id;
        var time = TypedTimeRegex().Match(t.Replace(" ", string.Empty));
        if (time.Success && (t.Contains(':') || t.Contains('h')))
        {
            var minutes = int.Parse(time.Groups[1].Value) * 60 + (time.Groups[2].Success ? int.Parse(time.Groups[2].Value) : 0);
            var id = $"slot:{minutes}";
            if (s.Options.Any(o => o.Id == id)) return id;
        }
        foreach (var o in s.Options)
        {
            var title = BotFormat.Normalize(o.Title);
            if (title == t || (t.Length >= 4 && title.StartsWith(t, StringComparison.Ordinal))) return o.Id;
        }
        return null;
    }

    /* ---------- Novo agendamento ---------- */

    private async Task AskServiceAsync(Ctx ctx, bool greet = true)
    {
        var s = ctx.S;
        var client = await ClientAsync(ctx);
        var more = new List<BotOption>();
        if (client is { Stats.Upcoming: > 0 }) more.Add(new BotOption("nav:my", "Meus agendamentos", "ver, remarcar ou cancelar"));
        more.Add(new BotOption("nav:info", "Endereço e horários", "como chegar, horário de funcionamento e preços"));
        more.Add(new BotOption("nav:attendant", "Falar com a equipe", "atendimento com uma pessoa"));

        var services = ctx.Cfg.Services.Where(x => x.Active).Take(CloudLimits.ListRows - more.Count).ToList();
        if (services.Count == 0)
        {
            s.Reset();
            await TextAsync(ctx, "No momento não há serviços para agendar por aqui. Digite *atendente* para falar com a equipe.");
            return;
        }
        var body = "Qual serviço você quer agendar?";
        if (greet)
        {
            var expired = ctx.Expired ? "Sua conversa anterior expirou, vamos recomeçar. " : string.Empty;
            var greeting = BotFormat.Fill(ctx.Cfg.Texts.GreetingText, new Dictionary<string, string>
            {
                ["nome"] = Name(ctx),
                ["estetica"] = ctx.Cfg.BusinessName,
            });
            body = $"{expired}{greeting}\n\n{body}";
        }
        s.State = BotStates.ChoosingService;
        await ListAsync(ctx, new ListMessage(
            body,
            "Ver serviços",
            [
                new BotListSection("Serviços", services
                    .Select(sv => new BotOption($"svc:{sv.Id}", BotFormat.Truncate(sv.Name, 24), BotFormat.Truncate(DescribeService(sv, ctx.Cfg), 72)))
                    .ToList()),
                new BotListSection("Mais opções", more),
            ]));
    }

    private async Task OnServiceAsync(Ctx ctx)
    {
        var id = Selected(ctx);
        var svc = id is not null && id.StartsWith("svc:", StringComparison.Ordinal) ? ctx.Cfg.ServiceById(id[4..]) : null;
        if (svc is null || !svc.Active) { await InvalidAsync(ctx); return; }
        if (await RefuseNewBookingAsync(ctx)) return;
        ctx.S.ServiceId = svc.Id;
        ctx.S.InvalidCount = 0;
        if (await AskRegisteredVehicleAsync(ctx)) return;
        await AskVehicleTypeAsync(ctx);
    }

    /// <summary>Cliente com carros cadastrados escolhe um deles (tipo, modelo e placa já vêm do cadastro).</summary>
    private async Task<bool> AskRegisteredVehicleAsync(Ctx ctx)
    {
        var svc = Service(ctx);
        var client = await ClientAsync(ctx);
        if (svc is null || client is null) return false;
        var vehicles = client.Vehicles.Where(v => ctx.Cfg.VehicleEnabled(v.VehicleType)).Take(CloudLimits.ListRows - 1).ToList();
        if (vehicles.Count == 0) return false;

        var rows = vehicles.Select(v =>
        {
            var price = svc.PriceFor(v.VehicleType);
            var desc = $"{ctx.Cfg.VehicleLabel(v.VehicleType)} · {BotFormat.Duration(svc.DurationFor(v.VehicleType))}" +
                       (ctx.Cfg.Bot.ShowPrices && price is not null ? $" · {BotFormat.Money(price)}" : string.Empty);
            return new BotOption($"veh:{v.Id}", BotFormat.Truncate(VehicleTitle(v.Vehicle, v.Plate), 24), BotFormat.Truncate(desc, 72));
        }).ToList();
        rows.Add(new BotOption("veh:other", "Outro veículo", "informar outro carro ou moto"));
        ctx.S.State = BotStates.ChoosingVehicle;
        await ListAsync(ctx, new ListMessage($"*{svc.Name}* ✨\n\nPara qual veículo?", "Ver veículos", [new BotListSection("Seus veículos", rows)]));
        return true;
    }

    private async Task OnVehicleChoiceAsync(Ctx ctx)
    {
        var s = ctx.S;
        var id = Selected(ctx);
        if (id == "veh:other") { s.InvalidCount = 0; await AskVehicleTypeAsync(ctx); return; }
        var client = await ClientAsync(ctx);
        var v = id is not null && id.StartsWith("veh:", StringComparison.Ordinal)
            ? client?.Vehicles.FirstOrDefault(x => x.Id == id[4..] && ctx.Cfg.VehicleEnabled(x.VehicleType))
            : null;
        if (v is null) { await InvalidAsync(ctx); return; }
        s.VehicleId = v.Id;
        s.VehicleType = v.VehicleType;
        s.Vehicle = v.Vehicle;
        s.Plate = v.Plate;
        s.InvalidCount = 0;
        await AskExtrasOrDayAsync(ctx);
    }

    private async Task AskVehicleTypeAsync(Ctx ctx)
    {
        var svc = Service(ctx);
        if (svc is null) { await AskServiceAsync(ctx); return; }
        var types = ctx.Cfg.EnabledVehicleTypes;
        if (types.Count == 1)
        {
            // Só um tipo atendido: não pergunta.
            ctx.S.VehicleType = types[0];
            await AskExtrasOrDayAsync(ctx);
            return;
        }
        ctx.S.State = BotStates.ChoosingVehicleType;
        var showPrices = ctx.Cfg.Bot.ShowPrices;
        var rows = types.Select(vt =>
        {
            var price = svc.PriceFor(vt);
            var desc = BotFormat.Duration(svc.DurationFor(vt)) +
                       (showPrices ? " · " + (price is null ? "preço sob consulta" : BotFormat.Money(price)) : string.Empty);
            return new BotOption($"vt:{vt}", BotFormat.Truncate(ctx.Cfg.VehicleLabel(vt), 24), desc);
        }).ToList();
        await ListAsync(ctx, new ListMessage(
            $"*{svc.Name}* ✨\n\nQual é o tipo do seu veículo? {(showPrices ? "A duração e o preço mudam" : "A duração muda")} conforme o tipo.",
            "Tipo de veículo",
            [new BotListSection("Tipo de veículo", rows)]));
    }

    private async Task OnVehicleTypeAsync(Ctx ctx)
    {
        var id = Selected(ctx);
        var vt = id is not null && id.StartsWith("vt:", StringComparison.Ordinal)
            ? AgendaConfigModel.AllVehicleTypes.FirstOrDefault(t => string.Equals(t, id[3..], StringComparison.OrdinalIgnoreCase))
            : null;
        if (vt is null || !ctx.Cfg.VehicleEnabled(vt)) { await InvalidAsync(ctx); return; }
        ctx.S.VehicleType = vt;
        ctx.S.VehicleId = null;
        ctx.S.InvalidCount = 0;
        await AskExtrasOrDayAsync(ctx);
    }

    /* ---------- Adicionais ---------- */

    private const int MaxBotExtras = 3;

    /// <summary>Adicionais ativos do catálogo que ainda dá para escolher.</summary>
    private static List<ExtraModel> RemainingExtras(Ctx ctx) =>
        ctx.Cfg.Extras.Where(x => x.Active && !ctx.S.ExtraIds.Contains(x.Id)).ToList();

    private Task AskExtrasOrDayAsync(Ctx ctx) =>
        ctx.Cfg.Bot.OfferExtras && ctx.S.RescheduleOf is null && ctx.S.ExtraIds.Count < MaxBotExtras && RemainingExtras(ctx).Count > 0
            ? AskExtrasAsync(ctx)
            : AskDayAsync(ctx);

    private async Task AskExtrasAsync(Ctx ctx)
    {
        var s = ctx.S;
        var vt = s.VehicleType ?? "Hatch";
        var rows = RemainingExtras(ctx).Take(CloudLimits.ListRows - 1).Select(x =>
        {
            var price = x.PriceFor(vt);
            var minutes = x.DurationFor(vt);
            var desc = string.Join(" · ", new[]
            {
                minutes > 0 ? $"+{BotFormat.Duration(minutes)}" : string.Empty,
                ctx.Cfg.Bot.ShowPrices && price is not null ? $"+{BotFormat.Money(price)}" : string.Empty,
            }.Where(p => p.Length > 0));
            return new BotOption($"ext:{x.Id}", BotFormat.Truncate(x.Name, 24), desc.Length == 0 ? null : desc);
        }).ToList();
        var chosen = ChosenExtras(ctx);
        rows.Add(chosen.Count == 0
            ? new BotOption("ext:done", "Sem adicionais", "seguir para a escolha do dia")
            : new BotOption("ext:done", "Pronto, escolher o dia", "seguir sem mais adicionais"));
        s.State = BotStates.ChoosingExtras;
        var body = chosen.Count == 0
            ? "Quer incluir algum adicional no serviço?"
            : $"Incluído: *{string.Join(", ", chosen.Select(x => x.Name))}*.\n\nMais algum?";
        await ListAsync(ctx, new ListMessage(body, "Ver adicionais", [new BotListSection("Adicionais", rows)]));
    }

    private async Task OnExtrasAsync(Ctx ctx)
    {
        var s = ctx.S;
        var t = BotFormat.Normalize(ctx.Msg.Text);
        var id = No.Contains(t) || t is "sem" or "nenhum" or "pronto" or "so isso" ? "ext:done" : Selected(ctx);
        if (id == "ext:done") { s.InvalidCount = 0; await AskDayAsync(ctx); return; }
        var extra = id is not null && id.StartsWith("ext:", StringComparison.Ordinal) ? ctx.Cfg.ExtraById(id[4..]) : null;
        if (extra is not { Active: true }) { await InvalidAsync(ctx); return; }
        if (!s.ExtraIds.Contains(extra.Id)) s.ExtraIds.Add(extra.Id);
        s.InvalidCount = 0;
        await AskExtrasOrDayAsync(ctx);
    }

    /// <summary>Adicionais escolhidos (do catálogo ou, numa remarcação, também serviços somados no balcão).</summary>
    private static List<BookingExtraDto> ChosenExtras(Ctx ctx)
    {
        var svc = Service(ctx);
        return svc is null || ctx.S.ExtraIds.Count == 0
            ? []
            : BookingExtras.Resolve(ctx.Cfg, svc, ctx.S.VehicleType ?? "Hatch", ctx.S.ExtraIds, skipMissing: true);
    }

    /// <summary>Tempo e preço dos adicionais escolhidos no tipo de veículo.</summary>
    private static (int Minutes, decimal Price, bool AnyPrice) ExtrasCost(Ctx ctx, string vt)
    {
        var chosen = ChosenExtras(ctx);
        return (BookingExtras.Duration(chosen), BookingExtras.Price(chosen), chosen.Any(x => x.Price is not null));
    }

    private async Task AskDayAsync(Ctx ctx, string intro = "")
    {
        var s = ctx.S;
        var svc = Service(ctx);
        var vt = s.VehicleType;
        if (svc is null || vt is null) { await AskServiceAsync(ctx); return; }
        s.State = BotStates.ChoosingDay;
        s.Date = null;
        s.SlotStart = null;
        s.SlotPage = 0;

        var rows = new List<BotOption>();
        var days = Math.Max(1, ctx.Cfg.Bot.DaysAhead);
        for (var i = 0; i < days && rows.Count < CloudLimits.ListRows; i++)
        {
            var d = BotFormat.Iso(ctx.Now.Date.AddDays(i));
            var slots = await AvailabilityAsync(ctx, d);
            if (slots.Count == 0) continue;
            var title = i switch
            {
                0 => $"Hoje · {BotFormat.DayLabel(d)}",
                1 => $"Amanhã · {BotFormat.DayLabel(d)}",
                _ => BotFormat.DayLabel(d),
            };
            var free = slots.Count == 1 ? "1 horário livre" : $"{slots.Count} horários livres";
            rows.Add(new BotOption($"day:{d}", title, $"{free} · a partir das {BotFormat.Hm(slots[0].Start)}"));
        }

        var duration = BotFormat.Duration(svc.DurationFor(vt) + ExtrasCost(ctx, vt).Minutes);
        if (rows.Count == 0)
        {
            s.Reset();
            await TextAsync(ctx,
                $"Poxa, não encontrei horário livre para *{svc.Name}* ({duration}) nos próximos {ctx.Cfg.Bot.DaysAhead} dias 😕\n" +
                "Digite *atendente* para a equipe ver um encaixe, ou *menu* para escolher outro serviço.");
            return;
        }
        var vehicle = s.VehicleId is not null ? VehicleTitle(s.Vehicle, s.Plate) : ctx.Cfg.VehicleLabel(vt);
        await ListAsync(ctx, new ListMessage(
            $"{intro}*{svc.Name}* · {vehicle} · {duration}\n\nEscolha o dia:",
            "Ver dias",
            [new BotListSection("Dias com horário livre", rows)]));
    }

    private async Task OnDayAsync(Ctx ctx)
    {
        var id = Selected(ctx);
        var date = id is not null && id.StartsWith("day:", StringComparison.Ordinal) ? id[4..] : null;
        if (date is null || !IsoDateRegex().IsMatch(date) || string.CompareOrdinal(date, ctx.Today) < 0) { await InvalidAsync(ctx); return; }
        ctx.S.Date = date;
        ctx.S.SlotPage = 0;
        ctx.S.InvalidCount = 0;
        await AskSlotAsync(ctx);
    }

    private async Task AskSlotAsync(Ctx ctx, string intro = "")
    {
        var s = ctx.S;
        var date = s.Date;
        if (date is null) { await AskDayAsync(ctx); return; }
        var slots = await AvailabilityAsync(ctx, date);
        if (slots.Count == 0)
        {
            await AskDayAsync(ctx, $"Os horários de {BotFormat.DayLabel(date)} acabaram de ser ocupados 😕 Escolha outro dia:\n\n");
            return;
        }
        var pages = (slots.Count + SlotsPerPage - 1) / SlotsPerPage;
        s.SlotPage = Math.Clamp(s.SlotPage, 0, pages - 1);
        var page = slots.Skip(s.SlotPage * SlotsPerPage).Take(SlotsPerPage).ToList();
        var rows = page.Select(x => new BotOption($"slot:{x.Start}", BotFormat.Hm(x.Start), $"termina às {BotFormat.Hm(x.End)}")).ToList();
        if (s.SlotPage < pages - 1) rows.Add(new BotOption("nav:more", "Mais horários ›", $"depois das {BotFormat.Hm(page[^1].Start)}"));
        rows.Add(new BotOption("nav:days", "‹ Outro dia", "voltar para a lista de dias"));
        s.State = BotStates.ChoosingSlot;
        await ListAsync(ctx, new ListMessage(
            $"{intro}Horários livres em *{BotFormat.DayLong(date)}*:",
            "Ver horários",
            [new BotListSection(BotFormat.DayLabel(date), rows)]));
    }

    private async Task OnSlotAsync(Ctx ctx)
    {
        var s = ctx.S;
        var id = Selected(ctx);
        if (id == "nav:more") { s.SlotPage += 1; await AskSlotAsync(ctx); return; }
        if (id == "nav:days") { await AskDayAsync(ctx); return; }
        var m = id is null ? null : SlotRegex().Match(id);
        var date = s.Date;
        if (m is not { Success: true } || date is null) { await InvalidAsync(ctx); return; }
        var start = int.Parse(m.Groups[1].Value);
        var slots = await AvailabilityAsync(ctx, date);
        if (!slots.Any(x => x.Start == start)) { await AskSlotAsync(ctx, "Esse horário não está mais livre 😕 Escolha outro:\n\n"); return; }
        s.SlotStart = start;
        s.InvalidCount = 0;
        if (s.Vehicle is null && !ctx.Cfg.Bot.AskVehicle)
        {
            s.Vehicle = string.Empty;
            s.Plate = string.Empty;
        }
        if (s.RescheduleOf is not null || s.Vehicle is not null) await AskConfirmAsync(ctx);
        else await AskVehicleAsync(ctx);
    }

    private Task AskVehicleAsync(Ctx ctx)
    {
        ctx.S.State = BotStates.AskingVehicle;
        return ButtonsAsync(ctx, new ButtonsMessage(
            "Quase lá! Qual o *modelo* e a *placa* do veículo?\nEx.: _Onix ABC1D23_",
            [new BotOption("veh:skip", "Prefiro não informar")]));
    }

    private async Task OnVehicleAsync(Ctx ctx)
    {
        var s = ctx.S;
        if (Selected(ctx) == "veh:skip")
        {
            s.Vehicle = string.Empty;
            s.Plate = string.Empty;
            await AskConfirmAsync(ctx);
            return;
        }
        var text = ctx.Msg.Text?.Trim() ?? string.Empty;
        if (text.Length == 0) { await InvalidAsync(ctx); return; }
        var (vehicle, plate) = BotFormat.ExtractVehicle(text);
        if (vehicle.Length == 0 && plate.Length == 0) { await InvalidAsync(ctx); return; }
        s.Vehicle = BotFormat.Truncate(vehicle, 40);
        s.Plate = plate;
        s.InvalidCount = 0;
        await AskConfirmAsync(ctx);
    }

    private async Task AskConfirmAsync(Ctx ctx)
    {
        var s = ctx.S;
        var svc = Service(ctx);
        var vt = s.VehicleType;
        if (svc is null || vt is null || s.Date is null || s.SlotStart is null) { await AskServiceAsync(ctx); return; }
        s.State = BotStates.Confirming;
        var extras = ExtrasCost(ctx, vt);
        var duration = svc.DurationFor(vt) + extras.Minutes;
        var main = svc.PriceFor(vt);
        decimal? price = main is null && !extras.AnyPrice ? null : (main ?? 0m) + extras.Price;
        var vehicle = VehicleTitle(s.Vehicle, s.Plate);
        var lines = new List<string>
        {
            s.RescheduleOf is not null ? "*Novo horário*" : "*Resumo do agendamento*",
            $"🚗 {svc.Name} ({ctx.Cfg.VehicleLabel(vt)})",
        };
        var chosen = ChosenExtras(ctx);
        if (chosen.Count > 0) lines.Add($"➕ {string.Join(", ", chosen.Select(x => x.Name))}");
        lines.Add($"📅 {BotFormat.DayLong(s.Date)}");
        lines.Add($"🕘 {BotFormat.Hm(s.SlotStart.Value)} às {BotFormat.Hm(s.SlotStart.Value + duration)}");
        if (vehicle.Length > 0) lines.Add($"🔖 {vehicle}");

        var plan = await PlanLineAsync(ctx, svc, vt, s.Date);
        if (plan.Covered)
        {
            lines.Add(plan.Line!);
            // O plano cobre o serviço; os adicionais são cobrados à parte.
            if (extras.Price > 0 && ctx.Cfg.Bot.ShowPrices) lines.Add($"💰 Adicionais: {BotFormat.Money(extras.Price)}");
        }
        else
        {
            if (price is not null && ctx.Cfg.Bot.ShowPrices) lines.Add($"💰 {BotFormat.Money(price)}");
            if (plan.Line is not null) lines.Add(plan.Line);
        }
        await ButtonsAsync(ctx, new ButtonsMessage(
            $"{string.Join("\n", lines)}\n\nPosso confirmar?",
            [new BotOption("confirm:ok", "Confirmar"), new BotOption("confirm:change", "Trocar horário"), new BotOption("confirm:abort", "Cancelar")]));
    }

    /// <summary>Linha do plano de fidelidade no resumo: incluso (e qual vez do mês) ou já usado no mês.</summary>
    private async Task<(bool Covered, string? Line)> PlanLineAsync(Ctx ctx, ServiceModel svc, string vt, string date)
    {
        var client = await ClientAsync(ctx);
        if (client is null || !Guid.TryParse(client.Id, out var clientId)) return (false, null);
        var s = ctx.S;
        if (s.RescheduleOf is not null)
        {
            // A remarcação leva o plano junto quando continua no mesmo mês.
            var old = await FindBookingAsync(ctx, s.RescheduleOf);
            if (old?.ClientPlanId is not null && old.Date[..7] == date[..7]) return (true, "💳 Continua incluso no seu plano");
        }
        var coverage = await agenda.PlanCoverageAsync(
            ctx.BranchId, clientId, Guid.TryParse(s.VehicleId, out var vid) ? vid : null, vt, svc.Id, date, ctx.Ct);
        if (coverage is null) return (false, null);
        var month = BotFormat.MonthName(date);
        return coverage.HasRoom
            ? (true, $"💳 Incluso no seu plano *{coverage.PlanName}* ({coverage.Used + 1}º de {coverage.TimesPerMonth} em {month})")
            : (false, $"💳 Seu plano *{coverage.PlanName}* já usou as {coverage.TimesPerMonth} vezes de {month}; este sai pelo preço normal.");
    }

    private async Task OnConfirmAsync(Ctx ctx)
    {
        var id = Yes.Contains(BotFormat.Normalize(ctx.Msg.Text)) ? "confirm:ok" : Selected(ctx);
        switch (id)
        {
            case "confirm:change": await AskDayAsync(ctx); return;
            case "confirm:abort": await CancelFlowAsync(ctx); return;
            case "confirm:ok": await BookAsync(ctx); return;
            default: await InvalidAsync(ctx); return;
        }
    }

    private async Task BookAsync(Ctx ctx)
    {
        var s = ctx.S;
        var svc = Service(ctx);
        var vt = s.VehicleType;
        var date = s.Date;
        var start = s.SlotStart;
        if (svc is null || vt is null || date is null || start is null) { await AskServiceAsync(ctx); return; }
        var rescheduleOf = s.RescheduleOf;
        if (rescheduleOf is null && await RefuseNewBookingAsync(ctx)) return;
        try
        {
            BookingDto booking;
            var tail = string.Empty;
            if (rescheduleOf is not null)
            {
                var old = await FindBookingAsync(ctx, rescheduleOf);
                booking = await agenda.RescheduleAsync(ctx.BranchId, Guid.Parse(rescheduleOf),
                    new RescheduleRequest { Date = date, Start = start.Value }, ctx.Ct);
                if (old is not null) tail = $"\n\nO horário anterior ({BotFormat.DayLabel(old.Date)} às {BotFormat.Hm(old.Start)}) foi liberado.";
            }
            else
            {
                var client = await ClientAsync(ctx);
                var optIn = client?.ReminderOptIn ?? true;
                booking = await agenda.CreateBookingAsync(ctx.BranchId, new NewBookingRequest
                {
                    Date = date,
                    Start = start.Value,
                    ServiceId = svc.Id,
                    VehicleType = vt,
                    // Sem modelo nem placa fica vazio: um nome genérico viraria um "carro" no cadastro do cliente.
                    Vehicle = s.Vehicle ?? string.Empty,
                    Plate = s.Plate ?? string.Empty,
                    ClientName = ClientName(ctx),
                    Phone = s.Phone,
                    Origin = "whatsapp",
                    ReminderOptIn = optIn,
                    ClientId = client?.Id,
                    VehicleId = client is null ? null : s.VehicleId,
                    ExtraIds = s.ExtraIds.ToList(),
                }, ctx.Ct);
                if (optIn)
                    tail = "\n\nVocê vai receber um lembrete na véspera para confirmar. Se não quiser lembretes, responda *parar lembretes*.";
            }
            if (booking.ClientPlanId is not null) tail = "\n💳 Incluso no seu plano." + tail;
            logger.LogInformation("Bot {Phone}: {Action} {BookingId}", s.Phone, rescheduleOf is null ? "agendou" : "remarcou", booking.Id);
            s.Reset();
            if (rescheduleOf is not null)
            {
                await TextAsync(ctx,
                    $"Remarcado! ✅\n🚗 {booking.ServiceName}\n📅 {BotFormat.DayLong(booking.Date)}, das {BotFormat.Hm(booking.Start)} às {BotFormat.Hm(booking.End)}{tail}");
                return;
            }
            var values = BotFormat.BookingValues(booking, ctx.Cfg.BusinessName, ctx.Cfg.BoxName(booking.BoxId));
            await TextAsync(ctx, BotFormat.Fill(ctx.Cfg.Texts.BookedText, values) + tail);
        }
        catch (AgendaConflictException)
        {
            s.SlotStart = null;
            await AskSlotAsync(ctx, "Esse horário acabou de ser ocupado 😕 Escolha outro:\n\n");
        }
    }

    /// <summary>
    /// Proteções do agendamento automático: cliente bloqueado, faltas recentes e limite de horários futuros.
    /// Verdadeiro = recusou (e já respondeu).
    /// </summary>
    private async Task<bool> RefuseNewBookingAsync(Ctx ctx)
    {
        var client = await ClientAsync(ctx);
        if (client is null) return false;
        var bot = ctx.Cfg.Bot;
        var upcoming = await UpcomingAsync(ctx);

        string? refusal = null;
        if (client.BotBlocked)
        {
            refusal = "Para este número, os agendamentos são feitos com a equipe. 🙏";
        }
        else if (bot.NoShowLimit > 0)
        {
            var since = ctx.Now.Date.AddDays(-Math.Max(1, bot.NoShowDays));
            var noShows = (await HistoryAsync(ctx)).Count(b => b.Status == "no_show" && BotFormat.ParseDate(b.Date) >= since);
            if (noShows >= bot.NoShowLimit)
                refusal = $"Como {(noShows == 1 ? "houve 1 falta recente" : $"houve {noShows} faltas recentes")}, " +
                          "os próximos horários são marcados com a equipe. 🙏";
        }

        if (refusal is null && bot.MaxUpcoming > 0 && upcoming.Count >= bot.MaxUpcoming)
        {
            logger.LogInformation("Bot {Phone}: limite de {Max} horários futuros", ctx.S.Phone, bot.MaxUpcoming);
            await ShowMyBookingsAsync(ctx,
                $"Você já tem {(upcoming.Count == 1 ? "1 horário marcado" : $"{upcoming.Count} horários marcados")} " +
                $"(o limite por aqui é {bot.MaxUpcoming}). Para marcar outro, remarque ou cancele um deles.\n\n");
            return true;
        }
        if (refusal is null) return false;

        ctx.S.Reset();
        logger.LogInformation("Bot {Phone}: agendamento recusado ({Reason})", ctx.S.Phone, client.BotBlocked ? "bloqueado" : "faltas");
        await ButtonsAsync(ctx, new ButtonsMessage(refusal, upcoming.Count > 0
            ? [new BotOption("nav:attendant", "Falar com a equipe"), new BotOption("nav:my", "Meus agendamentos")]
            : [new BotOption("nav:attendant", "Falar com a equipe")]));
        return true;
    }

    /* ---------- Meus agendamentos ---------- */

    private async Task ShowMyBookingsAsync(Ctx ctx, string intro = "")
    {
        var s = ctx.S;
        var upcoming = await UpcomingAsync(ctx);
        s.Reset();
        if (upcoming.Count == 0)
        {
            await TextAsync(ctx, $"{intro}Você não tem horários marcados por aqui. 🙂");
            await AskServiceAsync(ctx, greet: false);
            return;
        }
        var rows = upcoming.Take(MaxListedBookings).Select(b =>
        {
            var desc = string.Join(" · ", new[] { b.ServiceName, VehicleTitle(b.Vehicle, b.Plate) }.Where(x => x.Length > 0)) +
                       (b.ClientPlanId is not null ? " · plano" : string.Empty);
            return new BotOption($"bk:{b.Id}", $"{BotFormat.DayLabel(b.Date)} · {BotFormat.Hm(b.Start)}", BotFormat.Truncate(desc, 72));
        }).ToList();
        rows.Add(new BotOption("nav:menu", "Agendar outro horário", "voltar para a lista de serviços"));
        s.State = BotStates.MyBookings;
        var more = upcoming.Count > MaxListedBookings ? $" (mostrando os {MaxListedBookings} primeiros)" : string.Empty;
        await ListAsync(ctx, new ListMessage(
            $"{intro}Seus próximos horários{more}. Toque num deles para remarcar ou cancelar:",
            "Ver horários",
            [new BotListSection("Meus agendamentos", rows)]));
    }

    private async Task OnMyBookingsAsync(Ctx ctx)
    {
        var id = Selected(ctx);
        if (id is not null && id.StartsWith("bk:", StringComparison.Ordinal)) { await ShowBookingActionsAsync(ctx, id[3..]); return; }
        await InvalidAsync(ctx);
    }

    private async Task ShowBookingActionsAsync(Ctx ctx, string bookingId)
    {
        var b = await OwnedScheduledBookingAsync(ctx, bookingId);
        if (b is null) return;
        var vehicle = VehicleTitle(b.Vehicle, b.Plate);
        var lines = new List<string>
        {
            $"*{b.ServiceName}*",
            $"📅 {BotFormat.DayLong(b.Date)}",
            $"🕘 {BotFormat.Hm(b.Start)} às {BotFormat.Hm(b.End)}",
        };
        if (vehicle.Length > 0) lines.Add($"🔖 {vehicle}");
        if (b.ClientPlanId is not null) lines.Add("💳 Incluso no seu plano");
        ctx.S.State = BotStates.BookingAction;
        await ButtonsAsync(ctx, new ButtonsMessage(
            $"{string.Join("\n", lines)}\n\nO que você quer fazer?",
            [new BotOption($"rem:{b.Id}", "Remarcar"), new BotOption($"cask:{b.Id}", "Cancelar horário"), new BotOption("nav:my", "Voltar")]));
    }

    private async Task OnBookingActionAsync(Ctx ctx)
    {
        var s = ctx.S;
        var t = BotFormat.Normalize(ctx.Msg.Text);
        var id = Yes.Contains(t) && s.Options.Count > 0 ? s.Options[0].Id
            : No.Contains(t) && s.Options.Any(o => o.Id == "nav:my") ? "nav:my"
            : Selected(ctx);
        if (id == "nav:my") { await ShowMyBookingsAsync(ctx); return; }
        var m = id is null ? null : ReminderRegex().Match(id);
        if (m is { Success: true }) { await OnBookingReplyAsync(ctx, m.Groups[1].Value, m.Groups[2].Value); return; }
        await InvalidAsync(ctx);
    }

    /* ---------- Respostas sobre um agendamento (lembrete D-1 e "meus agendamentos") ---------- */

    private async Task OnBookingReplyAsync(Ctx ctx, string kind, string bookingId)
    {
        var s = ctx.S;
        var b = await OwnedScheduledBookingAsync(ctx, bookingId);
        if (b is null) return;
        var when = $"{BotFormat.DayLong(b.Date)} às {BotFormat.Hm(b.Start)}";
        var id = Guid.Parse(b.Id);

        if (kind == "conf")
        {
            await agenda.RegisterAnswerAsync(ctx.BranchId, id, new AnswerRequest { Resposta = "confirmed", Em = Local(ctx.Now) }, ctx.Ct);
            s.Reset();
            var values = BotFormat.BookingValues(b, ctx.Cfg.BusinessName, ctx.Cfg.BoxName(b.BoxId));
            await TextAsync(ctx, BotFormat.Fill(ctx.Cfg.Texts.ConfirmedText, values));
            return;
        }

        // Prazo mínimo para cancelar/remarcar sozinho: dentro dele, só com a equipe.
        var limit = ctx.Cfg.Bot.CancelMinHours;
        var startsAt = BotFormat.ParseDate(b.Date).AddMinutes(b.Start);
        if (limit > 0 && startsAt - ctx.Now < TimeSpan.FromHours(limit))
        {
            s.Reset();
            await ButtonsAsync(ctx, new ButtonsMessage(
                $"Faltam menos de {limit} h para o seu horário ({when}), então {(kind == "rem" ? "a remarcação" : "o cancelamento")} " +
                "precisa ser feito com a equipe. 🙏",
                [new BotOption("nav:attendant", "Falar com atendente")]));
            return;
        }

        if (kind == "cask")
        {
            s.State = BotStates.BookingAction;
            await ButtonsAsync(ctx, new ButtonsMessage(
                $"Cancelar o horário de *{b.ServiceName}* em {when}?",
                [new BotOption($"canc:{b.Id}", "Sim, cancelar"), new BotOption("nav:my", "Não, manter")]));
            return;
        }

        if (kind == "canc")
        {
            await agenda.SetStatusAsync(ctx.BranchId, id, "canceled", ctx.Ct);
            await agenda.RegisterAnswerAsync(ctx.BranchId, id, new AnswerRequest { Resposta = "canceled", Em = Local(ctx.Now) }, ctx.Ct);
            s.Reset();
            await TextAsync(ctx, $"Cancelado. O horário de {when} foi liberado.\nSe quiser marcar outro, é só mandar *menu*.");
            return;
        }

        // Remarcar: o horário antigo só é cancelado quando o novo for reservado (mesma operação).
        await agenda.RegisterAnswerAsync(ctx.BranchId, id, new AnswerRequest { Resposta = "reschedule", Em = Local(ctx.Now) }, ctx.Ct);
        var svc = ctx.Cfg.ServiceById(b.ServiceId);
        if (svc is null || !svc.Active) { await ToAttendantAsync(ctx); return; }
        s.Reset();
        s.ServiceId = svc.Id;
        s.VehicleType = b.VehicleType;
        s.Vehicle = b.Vehicle;
        s.Plate = b.Plate;
        s.VehicleId = b.VehicleId;
        // Os adicionais vão junto com o horário; a busca de horários livres precisa do tempo deles.
        s.ExtraIds = (b.Extras ?? []).Select(x => x.Id).ToList();
        s.RescheduleOf = b.Id;
        await AskDayAsync(ctx, $"Sem problema! Seu horário atual ({when}) continua reservado até você escolher o novo.\n\n");
    }

    /// <summary>
    /// Agendamento do número e ainda marcado; senão responde o motivo e devolve nulo. Dono = mesmo telefone (com ou
    /// sem o nono dígito) ou agendamento do cliente cadastrado com este telefone. Sem essa checagem, um terceiro
    /// poderia cancelar o horário de outra pessoa.
    /// </summary>
    private async Task<BookingDto?> OwnedScheduledBookingAsync(Ctx ctx, string bookingId)
    {
        var b = await FindBookingAsync(ctx, bookingId);
        var client = b is null ? null : await ClientAsync(ctx);
        var owner = b is not null && (SamePhone(b.Phone, ctx.Msg.Phone) || (client is not null && b.ClientId == client.Id));
        if (b is null || !owner)
        {
            ctx.S.Reset();
            await TextAsync(ctx, "Não encontrei esse agendamento no seu número 🤔 Digite *menu* para agendar ou *atendente* para falar com a equipe.");
            return null;
        }
        if (b.Status != "scheduled")
        {
            ctx.S.Reset();
            await TextAsync(ctx,
                $"Esse agendamento está como *{ctx.Cfg.StatusText(b.Status).ToLowerInvariant()}*, então não há nada a fazer nele. " +
                "Para marcar outro horário, mande *menu*.");
            return null;
        }
        return b;
    }

    /* ---------- Informações ---------- */

    private async Task ShowInfoAsync(Ctx ctx)
    {
        await SendInfoAsync(ctx);
        ctx.S.Reset();
        await ButtonsAsync(ctx, new ButtonsMessage(
            "Quer agendar um horário?",
            [new BotOption("nav:menu", "Agendar horário"), new BotOption("nav:attendant", "Falar com a equipe")]));
    }

    /// <summary>Endereço, mapa, horário de funcionamento, preços (se o bot mostra preços), pagamento e recado.</summary>
    private Task SendInfoAsync(Ctx ctx)
    {
        var cfg = ctx.Cfg;
        var info = cfg.Info;
        var parts = new List<string> { $"*{cfg.BusinessName}*" };
        if (!string.IsNullOrWhiteSpace(info.Address)) parts.Add($"📍 {info.Address.Trim()}");
        if (!string.IsNullOrWhiteSpace(info.MapsUrl)) parts.Add($"🗺️ {info.MapsUrl.Trim()}");
        parts.Add("🕘 *Horário de funcionamento*\n" + string.Join("\n", BotFormat.OpeningHours(cfg.Hours)));
        if (cfg.Bot.ShowPrices)
        {
            var prices = cfg.Services.Where(x => x.Active).Select(sv =>
            {
                var values = cfg.EnabledVehicleTypes.Select(sv.PriceFor).Where(p => p is not null).Select(p => p!.Value).ToList();
                var price = values.Count == 0 ? "sob consulta"
                    : values.Min() == values.Max() ? BotFormat.Money(values.Min())
                    : $"a partir de {BotFormat.Money(values.Min())}";
                return $"• {sv.Name}: {price}";
            }).ToList();
            if (prices.Count > 0) parts.Add("💰 *Preços*\n" + string.Join("\n", prices));
        }
        if (!string.IsNullOrWhiteSpace(info.Payment)) parts.Add($"💳 {info.Payment.Trim()}");
        if (!string.IsNullOrWhiteSpace(info.Notes)) parts.Add(info.Notes.Trim());
        return TextAsync(ctx, BotFormat.Truncate(string.Join("\n\n", parts), CloudLimits.Text));
    }

    /* ---------- Comandos globais e erros ---------- */

    private Task ToAttendantAsync(Ctx ctx)
    {
        ctx.S.Reset();
        ctx.S.State = BotStates.WithAttendant;
        logger.LogInformation("Bot {Phone}: pediu atendente", ctx.S.Phone);
        return TextAsync(ctx, BotFormat.Fill(ctx.Cfg.Texts.AttendantText, new Dictionary<string, string>
        {
            ["nome"] = Name(ctx),
            ["estetica"] = ctx.Cfg.BusinessName,
        }));
    }

    private async Task CancelFlowAsync(Ctx ctx)
    {
        var inProgress = ctx.S.State is not (BotStates.Idle or BotStates.WithAttendant or BotStates.MyBookings or BotStates.BookingAction);
        ctx.S.Reset();
        if (inProgress)
        {
            await TextAsync(ctx, "Tudo bem, não agendei nada. Quando quiser, é só mandar *menu*. 👋");
            return;
        }
        if ((await UpcomingAsync(ctx)).Count > 0)
        {
            await ShowMyBookingsAsync(ctx, "Não há agendamento em andamento. Para cancelar ou remarcar um horário marcado, escolha abaixo.\n\n");
            return;
        }
        await TextAsync(ctx, "Não há agendamento em andamento nem horário marcado. Para marcar um horário, mande *menu*.");
    }

    private async Task SetReminderOptInAsync(Ctx ctx, bool on)
    {
        var existing = await ClientAsync(ctx);
        var name = existing?.Name is { Length: > 0 } n ? n : ctx.S.ProfileName ?? string.Empty;
        await agenda.UpsertClientAsync(ctx.BranchId, ctx.S.Phone,
            new ClientRequest { Phone = ctx.S.Phone, Name = name, ReminderOptIn = on, OptInAt = on ? Local(ctx.Now) : null }, ctx.Ct);
        await TextAsync(ctx, on
            ? "Combinado! Você volta a receber o lembrete na véspera dos seus horários. ✅"
            : "Pronto, você não vai mais receber lembretes por aqui. Para voltar a receber, responda *quero lembretes*.");
    }

    private Task OnUnsupportedAsync(Ctx ctx) =>
        ctx.S.State == BotStates.WithAttendant
            ? Task.CompletedTask
            : TextAsync(ctx, "Por aqui eu só entendo texto e as opções da lista 🙏 Toque numa opção ou escreva sua resposta. " +
                             "Para falar com a equipe, digite *atendente*.");

    private async Task InvalidAsync(Ctx ctx)
    {
        var s = ctx.S;
        s.InvalidCount += 1;
        if (s.InvalidCount >= Math.Max(1, ctx.Cfg.Bot.MaxInvalidAttempts))
        {
            s.InvalidCount = 0;
            await ButtonsAsync(ctx, new ButtonsMessage(
                "Acho que não estou conseguindo te ajudar por aqui 😅\nQuer falar com alguém da equipe?",
                [new BotOption("nav:attendant", "Falar com atendente"), new BotOption("nav:menu", "Recomeçar")]));
            return;
        }
        await TextAsync(ctx, "Não entendi 🤔 Toque numa das opções ou digite *menu* para recomeçar.");
        var invalid = s.InvalidCount;
        await (s.State switch
        {
            BotStates.ChoosingVehicle => RepeatVehicleChoiceAsync(ctx),
            BotStates.ChoosingVehicleType => AskVehicleTypeAsync(ctx),
            BotStates.ChoosingDay => AskDayAsync(ctx),
            BotStates.ChoosingSlot => AskSlotAsync(ctx),
            BotStates.AskingVehicle => AskVehicleAsync(ctx),
            BotStates.Confirming => AskConfirmAsync(ctx),
            BotStates.MyBookings or BotStates.BookingAction => ShowMyBookingsAsync(ctx),
            BotStates.ChoosingExtras => AskExtrasAsync(ctx),
            _ => AskServiceAsync(ctx, greet: false),
        });
        s.InvalidCount = invalid; // reabrir a lista não zera a contagem de erros
    }

    private async Task RepeatVehicleChoiceAsync(Ctx ctx)
    {
        if (!await AskRegisteredVehicleAsync(ctx)) await AskVehicleTypeAsync(ctx);
    }

    /* ---------- Apoio ---------- */

    private async Task<ClientDto?> ClientAsync(Ctx ctx)
    {
        if (!ctx.ClientLoaded)
        {
            ctx.Client = await agenda.GetClientAsync(ctx.BranchId, ctx.S.Phone, ctx.Ct);
            ctx.ClientLoaded = true;
        }
        return ctx.Client;
    }

    /// <summary>Agendamentos do cliente cadastrado com este telefone (do mais novo para o mais antigo).</summary>
    private async Task<IReadOnlyList<BookingDto>> HistoryAsync(Ctx ctx)
    {
        if (ctx.History is null)
        {
            var client = await ClientAsync(ctx);
            ctx.History = client is not null && Guid.TryParse(client.Id, out var id)
                ? await agenda.ClientBookingsAsync(ctx.BranchId, id, ctx.Ct)
                : [];
        }
        return ctx.History;
    }

    /// <summary>Horários marcados que ainda não começaram, do mais próximo ao mais distante.</summary>
    private async Task<IReadOnlyList<BookingDto>> UpcomingAsync(Ctx ctx) =>
        (await HistoryAsync(ctx))
            .Where(b => b.Status == "scheduled" && BotFormat.ParseDate(b.Date).AddMinutes(b.Start) > ctx.Now)
            .OrderBy(b => b.Date, StringComparer.Ordinal)
            .ThenBy(b => b.Start)
            .ToList();

    /// <summary>Primeiro nome: do cadastro, senão do perfil do WhatsApp.</summary>
    private static string Name(Ctx ctx)
    {
        var n = BotFormat.FirstName(ctx.Client?.Name is { Length: > 0 } registered ? registered : ctx.S.ProfileName);
        return n.Length == 0 ? "cliente" : n;
    }

    /// <summary>Nome da reserva: o do cadastro (o perfil do WhatsApp não sobrescreve), senão o do perfil.</summary>
    private static string ClientName(Ctx ctx) =>
        ctx.Client?.Name is { Length: > 0 } registered ? registered
        : !string.IsNullOrWhiteSpace(ctx.S.ProfileName) ? ctx.S.ProfileName!
        : "Cliente WhatsApp";

    private static string VehicleTitle(string? vehicle, string? plate) =>
        string.Join(" · ", new[] { vehicle, plate }.Where(x => !string.IsNullOrEmpty(x)));

    private static string Local(DateTime d) => AgendaFormat.LocalDateTime(d);

    private static ServiceModel? Service(Ctx ctx)
    {
        var svc = ctx.Cfg.ServiceById(ctx.S.ServiceId);
        return svc is { Active: true } ? svc : null;
    }

    private static bool SamePhone(string a, string b) =>
        a.Length > 0 && b.Length > 0 && AgendaFormat.PhoneVariants(a).Intersect(AgendaFormat.PhoneVariants(b)).Any();

    private async Task<BookingDto?> FindBookingAsync(Ctx ctx, string id)
    {
        if (!Guid.TryParse(id, out var guid)) return null;
        try
        {
            return await agenda.GetBookingAsync(ctx.BranchId, guid, ctx.Ct);
        }
        catch (AgendaNotFoundException)
        {
            return null;
        }
    }

    private Task<IReadOnlyList<SlotOptionDto>> AvailabilityAsync(Ctx ctx, string date) =>
        agenda.AvailabilityAsync(
            ctx.BranchId,
            date,
            ctx.S.ServiceId ?? string.Empty,
            ctx.S.VehicleType ?? "Hatch",
            Guid.TryParse(ctx.S.RescheduleOf, out var ignore) ? ignore : null,
            ctx.Cfg.Bot.MinNoticeMin,
            ctx.S.ExtraIds,
            ctx.Ct);

    private static string DescribeService(ServiceModel sv, AgendaConfigModel cfg)
    {
        var types = cfg.EnabledVehicleTypes;
        var durations = types.Select(sv.DurationFor).ToList();
        var minD = durations.Min();
        var maxD = durations.Max();
        var prices = types.Select(sv.PriceFor).Where(p => p is not null).Select(p => p!.Value).ToList();
        var duration = minD == maxD ? BotFormat.Duration(minD) : $"{BotFormat.Duration(minD)} a {BotFormat.Duration(maxD)}";
        if (!cfg.Bot.ShowPrices) return duration;
        var price = prices.Count == 0 ? "preço sob consulta" : $"a partir de {BotFormat.Money(prices.Min())}";
        return $"{duration} · {price}";
    }

    private Task TextAsync(Ctx ctx, string body) => SendAsync(ctx, new TextMessage(body));

    private Task ListAsync(Ctx ctx, ListMessage m)
    {
        ctx.S.Options = m.Rows.ToList();
        return SendAsync(ctx, m);
    }

    private Task ButtonsAsync(Ctx ctx, ButtonsMessage m)
    {
        ctx.S.Options = m.Buttons.ToList();
        return SendAsync(ctx, m);
    }

    private Task SendAsync(Ctx ctx, OutgoingMessage m) => SendAsync(ctx.BranchId, ctx.Msg.Phone, ctx.Channel, m, ctx.Ct);

    private async Task SendAsync(Guid branchId, string phone, IMessagingChannel channel, OutgoingMessage m, CancellationToken ct)
    {
        var wamId = await channel.SendAsync(branchId, phone, m, ct);
        await store.LogMessageAsync(branchId, phone, "out", m.Kind, m.Body, wamId, new BotLogExtra(BotDtoMapping.MetaJson(m)), ct);
    }

    [GeneratedRegex(@"^(conf|rem|canc|cask):(.+)$")]
    private static partial Regex ReminderRegex();

    [GeneratedRegex(@"^slot:(\d+)$")]
    private static partial Regex SlotRegex();

    [GeneratedRegex(@"^(\d{1,2})(?:[:h](\d{2})?)h?$")]
    private static partial Regex TypedTimeRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDateRegex();
}
