using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Agenda;
using Microsoft.Extensions.Logging;

namespace Application.Bot;

/// <summary>
/// Agendamento pelo WhatsApp: máquina de estados determinística, sem LLM (porta do BookingFlow do app).
///
///     idle → choosingService → choosingVehicleType → choosingDay → choosingSlot
///          → askingVehicle → confirming → (reserva) → idle
///
/// Regras transversais: sessão expira depois de Bot.SessionTimeoutMin parada; entrada inválida repete a pergunta
/// e, depois de N tentativas, oferece atendente; comandos globais (menu, cancelar, atendente, parar/quero lembretes);
/// mensagem repetida da Meta é ignorada pelo id; respostas do lembrete (conf/rem/canc) valem em qualquer passo e
/// conferem se o número é o dono do agendamento. As regras de agenda são as do <see cref="IAgendaService"/>.
/// </summary>
public sealed partial class BookingFlow(IAgendaService agenda, IBotStore store, IAgendaClock clock, ILogger<BookingFlow> logger)
{
    private const int SlotsPerPage = 8;
    private static readonly TimeSpan AttendantTimeout = TimeSpan.FromHours(12);

    private static readonly HashSet<string> CmdMenu = ["menu", "inicio", "comecar", "recomecar"];
    private static readonly HashSet<string> CmdCancel = ["cancelar", "sair", "parar agendamento"];
    private static readonly HashSet<string> CmdAttendant = ["atendente", "humano", "falar com atendente", "falar com alguem"];
    private static readonly HashSet<string> CmdOptOut = ["parar lembretes", "nao quero lembretes", "parar lembrete"];
    private static readonly HashSet<string> CmdOptIn = ["quero lembretes", "quero lembrete", "voltar lembretes"];
    private static readonly HashSet<string> Yes = ["sim", "ok", "confirmar", "confirmo", "pode", "pode confirmar", "s"];
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
        await store.LogMessageAsync(branchId, msg.Phone, "in", msg.Kind, DescribeIncoming(msg), msg.Id, ct);
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
        var sel = ctx.Msg.SelectionId;

        var reminder = sel is null ? null : ReminderRegex().Match(sel);
        if (reminder is { Success: true }) { await OnReminderReplyAsync(ctx, reminder.Groups[1].Value, reminder.Groups[2].Value); return; }

        var cmd = BotFormat.Normalize(ctx.Msg.Text);
        if (sel == "nav:menu" || CmdMenu.Contains(cmd)) { s.Reset(); await AskServiceAsync(ctx); return; }
        if (sel == "nav:attendant" || CmdAttendant.Contains(cmd)) { await ToAttendantAsync(ctx); return; }
        if (CmdCancel.Contains(cmd)) { await CancelFlowAsync(ctx); return; }
        if (CmdOptOut.Contains(cmd)) { await SetReminderOptInAsync(ctx, false); return; }
        if (CmdOptIn.Contains(cmd)) { await SetReminderOptInAsync(ctx, true); return; }

        switch (s.State)
        {
            case BotStates.WithAttendant: return; // a equipe responde manualmente
            case BotStates.ChoosingService: await OnServiceAsync(ctx); return;
            case BotStates.ChoosingVehicleType: await OnVehicleTypeAsync(ctx); return;
            case BotStates.ChoosingDay: await OnDayAsync(ctx); return;
            case BotStates.ChoosingSlot: await OnSlotAsync(ctx); return;
            case BotStates.AskingVehicle: await OnVehicleAsync(ctx); return;
            case BotStates.Confirming: await OnConfirmAsync(ctx); return;
            default: await AskServiceAsync(ctx); return;
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

    /* ---------- Passos ---------- */

    private async Task AskServiceAsync(Ctx ctx, bool greet = true)
    {
        var s = ctx.S;
        var services = ctx.Cfg.Services.Where(x => x.Active).Take(CloudLimits.ListRows).ToList();
        if (services.Count == 0)
        {
            s.Reset();
            await TextAsync(ctx, "No momento não há serviços para agendar por aqui. Digite *atendente* para falar com a equipe.");
            return;
        }
        var body = "Qual serviço você quer agendar?";
        if (greet)
        {
            var client = await agenda.GetClientAsync(ctx.BranchId, s.Phone, ctx.Ct);
            var name = BotFormat.FirstName(s.ProfileName ?? client?.Name);
            var expired = ctx.Expired ? "Sua conversa anterior expirou, vamos recomeçar. " : string.Empty;
            var greeting = BotFormat.Fill(ctx.Cfg.Texts.GreetingText, new Dictionary<string, string>
            {
                ["nome"] = name.Length == 0 ? "cliente" : name,
                ["estetica"] = ctx.Cfg.BusinessName,
            });
            body = $"{expired}{greeting}\n\n{body}";
        }
        s.State = BotStates.ChoosingService;
        await ListAsync(ctx, new ListMessage(
            body,
            "Ver serviços",
            [new BotListSection("Serviços", services
                .Select(sv => new BotOption($"svc:{sv.Id}", BotFormat.Truncate(sv.Name, 24), BotFormat.Truncate(DescribeService(sv, ctx.Cfg), 72)))
                .ToList())],
            Footer: "Digite atendente para falar com a equipe"));
    }

    private async Task OnServiceAsync(Ctx ctx)
    {
        var id = Selected(ctx);
        var svc = id is not null && id.StartsWith("svc:", StringComparison.Ordinal) ? ctx.Cfg.ServiceById(id[4..]) : null;
        if (svc is null || !svc.Active) { await InvalidAsync(ctx); return; }
        ctx.S.ServiceId = svc.Id;
        ctx.S.InvalidCount = 0;
        await AskVehicleTypeAsync(ctx);
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
            await AskDayAsync(ctx);
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
        ctx.S.InvalidCount = 0;
        await AskDayAsync(ctx);
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

        var duration = BotFormat.Duration(svc.DurationFor(vt));
        if (rows.Count == 0)
        {
            s.Reset();
            await TextAsync(ctx,
                $"Poxa, não encontrei horário livre para *{svc.Name}* ({duration}) nos próximos {ctx.Cfg.Bot.DaysAhead} dias 😕\n" +
                "Digite *atendente* para a equipe ver um encaixe, ou *menu* para escolher outro serviço.");
            return;
        }
        await ListAsync(ctx, new ListMessage(
            $"{intro}*{svc.Name}* · {ctx.Cfg.VehicleLabel(vt)} · {duration}\n\nEscolha o dia:",
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
        var duration = svc.DurationFor(vt);
        var price = svc.PriceFor(vt);
        var vehicle = string.Join(" · ", new[] { s.Vehicle, s.Plate }.Where(x => !string.IsNullOrEmpty(x)));
        var lines = new List<string>
        {
            s.RescheduleOf is not null ? "*Novo horário*" : "*Resumo do agendamento*",
            $"🚗 {svc.Name} ({ctx.Cfg.VehicleLabel(vt)})",
            $"📅 {BotFormat.DayLong(s.Date)}",
            $"🕘 {BotFormat.Hm(s.SlotStart.Value)} às {BotFormat.Hm(s.SlotStart.Value + duration)}",
        };
        if (vehicle.Length > 0) lines.Add($"🔖 {vehicle}");
        if (price is not null && ctx.Cfg.Bot.ShowPrices) lines.Add($"💰 {BotFormat.Money(price)}");
        await ButtonsAsync(ctx, new ButtonsMessage(
            $"{string.Join("\n", lines)}\n\nPosso confirmar?",
            [new BotOption("confirm:ok", "Confirmar"), new BotOption("confirm:change", "Trocar horário"), new BotOption("confirm:abort", "Cancelar")]));
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
                var client = await agenda.GetClientAsync(ctx.BranchId, s.Phone, ctx.Ct);
                var optIn = client?.ReminderOptIn ?? true;
                var vehicle = s.Vehicle ?? string.Empty;
                booking = await agenda.CreateBookingAsync(ctx.BranchId, new NewBookingRequest
                {
                    Date = date,
                    Start = start.Value,
                    ServiceId = svc.Id,
                    VehicleType = vt,
                    Vehicle = vehicle.Length == 0 ? "Veículo" : vehicle,
                    Plate = s.Plate ?? string.Empty,
                    ClientName = !string.IsNullOrWhiteSpace(s.ProfileName) ? s.ProfileName! : client?.Name is { Length: > 0 } n ? n : "Cliente WhatsApp",
                    Phone = s.Phone,
                    Origin = "whatsapp",
                    ReminderOptIn = optIn,
                }, ctx.Ct);
                if (optIn)
                    tail = "\n\nVocê vai receber um lembrete na véspera para confirmar. Se não quiser lembretes, responda *parar lembretes*.";
            }
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

    /* ---------- Lembrete D-1 ---------- */

    private async Task OnReminderReplyAsync(Ctx ctx, string kind, string bookingId)
    {
        var s = ctx.S;
        var b = await FindBookingAsync(ctx, bookingId);
        // Dono: sem esta checagem, um terceiro poderia cancelar o horário de outra pessoa.
        if (b is null || !SamePhone(b.Phone, ctx.Msg.Phone))
        {
            await TextAsync(ctx, "Não encontrei esse agendamento no seu número 🤔 Digite *menu* para agendar ou *atendente* para falar com a equipe.");
            return;
        }
        if (b.Status != "scheduled")
        {
            await TextAsync(ctx,
                $"Esse agendamento está como *{ctx.Cfg.StatusText(b.Status).ToLowerInvariant()}*, então não há nada a fazer nele. " +
                "Para marcar outro horário, mande *menu*.");
            return;
        }
        var when = $"{BotFormat.DayLong(b.Date)} às {BotFormat.Hm(b.Start)}";
        var id = Guid.Parse(b.Id);

        if (kind == "conf")
        {
            await agenda.RegisterAnswerAsync(ctx.BranchId, id, new AnswerRequest { Resposta = "confirmed", Em = Local(ctx.Now) }, ctx.Ct);
            var values = BotFormat.BookingValues(b, ctx.Cfg.BusinessName, ctx.Cfg.BoxName(b.BoxId));
            await TextAsync(ctx, BotFormat.Fill(ctx.Cfg.Texts.ConfirmedText, values));
            return;
        }

        // Prazo mínimo para cancelar/remarcar sozinho: dentro dele, só com a equipe.
        var limit = ctx.Cfg.Bot.CancelMinHours;
        var startsAt = BotFormat.ParseDate(b.Date).AddMinutes(b.Start);
        if (limit > 0 && startsAt - ctx.Now < TimeSpan.FromHours(limit))
        {
            await ButtonsAsync(ctx, new ButtonsMessage(
                $"Faltam menos de {limit} h para o seu horário ({when}), então {(kind == "canc" ? "o cancelamento" : "a remarcação")} " +
                "precisa ser feito com a equipe. 🙏",
                [new BotOption("nav:attendant", "Falar com atendente")]));
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
        s.RescheduleOf = b.Id;
        await AskDayAsync(ctx, $"Sem problema! Seu horário atual ({when}) continua reservado até você escolher o novo.\n\n");
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

    private Task CancelFlowAsync(Ctx ctx)
    {
        var inProgress = ctx.S.State is not (BotStates.Idle or BotStates.WithAttendant);
        ctx.S.Reset();
        return TextAsync(ctx, inProgress
            ? "Tudo bem, não agendei nada. Quando quiser, é só mandar *menu*. 👋"
            : "Não há agendamento em andamento. Para marcar um horário, mande *menu*.\n" +
              "Para cancelar um horário já marcado, use o botão do lembrete ou digite *atendente*.");
    }

    private async Task SetReminderOptInAsync(Ctx ctx, bool on)
    {
        var existing = await agenda.GetClientAsync(ctx.BranchId, ctx.S.Phone, ctx.Ct);
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
        await (s.State switch
        {
            BotStates.ChoosingVehicleType => AskVehicleTypeAsync(ctx),
            BotStates.ChoosingDay => AskDayAsync(ctx),
            BotStates.ChoosingSlot => AskSlotAsync(ctx),
            BotStates.AskingVehicle => AskVehicleAsync(ctx),
            BotStates.Confirming => AskConfirmAsync(ctx),
            _ => AskServiceAsync(ctx, greet: false),
        });
    }

    /* ---------- Apoio ---------- */

    private static string Name(Ctx ctx)
    {
        var n = BotFormat.FirstName(ctx.S.ProfileName);
        return n.Length == 0 ? "cliente" : n;
    }

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
        await store.LogMessageAsync(branchId, phone, "out", m.Kind, m.Body, wamId, ct);
    }

    [GeneratedRegex(@"^(conf|rem|canc):(.+)$")]
    private static partial Regex ReminderRegex();

    [GeneratedRegex(@"^slot:(\d+)$")]
    private static partial Regex SlotRegex();

    [GeneratedRegex(@"^(\d{1,2})(?:[:h](\d{2})?)h?$")]
    private static partial Regex TypedTimeRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDateRegex();
}
