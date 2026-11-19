"""Monta a "Filial Zona Sul" com dados de demonstração para tirar prints do app (banco local de desenvolvimento).

Uso: python infra/demo/dados_demo.py [http://localhost:5000]

Apaga TUDO da Zona Sul (agendamentos, clientes, planos, lista de espera, conversas) e recria: clientes com carros, um
dia de hoje em andamento (montado em volta da hora em que o script roda), amanhã com lembretes, semanas anteriores
(relatórios e clientes sumidos), plano de fidelidade com mensalidade paga e em atraso, conversas do WhatsApp (agendamento
pelo bot, atendente, pergunta de preço, avaliação), agendamento recorrente, lista de espera num sábado lotado e a
funcionária "marina" (perfil Equipe, senha provisória Marina2026, que o app pede para trocar).

Atendimentos em horas ou dias que já passaram não podem ser marcados pela API: são criados semanas à frente (mesmo dia
da semana) e trazidos para a data certa direto no banco (sqlcmd, autenticação do Windows). Só para desenvolvimento.
"""
import datetime as dt
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
BRANCH_NAME = "Filial Zona Sul"
SQL = ["sqlcmd", "-S", "localhost,1433", "-E", "-d", "AgendaGhost", "-b", "-I", "-h", "-1", "-W"]
NOW = dt.datetime.now()
DEMO_HORA = os.environ.get("DEMO_HORA")  # "15:30": mesmo relógio de demonstração da API (Agenda:RelogioDemo)
if DEMO_HORA:
    NOW = dt.datetime.combine(NOW.date(), dt.time.fromisoformat(DEMO_HORA))
TODAY = NOW.date()
SHIFT_WEEKS = 26  # criados 26 semanas à frente (mais que o histórico mais antigo) e trazidos de volta
UTC_OFFSET = dt.timedelta(hours=3)  # America/Sao_Paulo


def call(method, path, body=None, ok=(200, 201, 204)):
    req = urllib.request.Request(BASE + path, data=None if body is None else json.dumps(body).encode(), method=method)
    req.add_header("Content-Type", "application/json")
    if TOKEN:
        req.add_header("Authorization", "Bearer " + TOKEN)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            status, raw = r.status, r.read()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read()
    data = json.loads(raw) if raw and raw[:1] in (b"{", b"[") else raw.decode("utf-8", "replace")
    if status not in ok:
        raise RuntimeError(f"{method} {path} -> {status}: {data}")
    return data


def sql(text):
    r = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON; " + text], capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        raise RuntimeError(f"sqlcmd: {r.stdout} {r.stderr}")
    return r.stdout.strip()


def iso(d):
    return d.isoformat()


def hm(m):
    return f"{m // 60:02d}:{m % 60:02d}"


if not DEMO_HORA and (NOW.hour >= 23 or NOW.hour < 8):
    sys.exit("Rode entre 8h e 23h: o dia de hoje da demonstração é montado em volta da hora atual "
             "(atendimentos feitos, um em andamento e os próximos).")

TOKEN = None
TOKEN = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"})["accessToken"]
branch = next(b for b in call("GET", "/api/agenda/filiais") if b["name"] == BRANCH_NAME)
BID = branch["id"]
A = f"/api/agenda/{BID}"
print("filial:", BRANCH_NAME, BID, "| agora:", NOW.strftime("%d/%m %H:%M"))

# ---------------------------------------------------------------- limpeza
sql(f"""
DECLARE @b uniqueidentifier = '{BID}';
DELETE FROM AgendaAudit WHERE BranchId = @b;
DELETE FROM AgendaReminders WHERE BranchId = @b;
DELETE FROM AgendaWaitlist WHERE BranchId = @b;
DELETE FROM AgendaPlanNotices WHERE BranchId = @b;
DELETE FROM AgendaPlanPayments WHERE BranchId = @b;
UPDATE AgendaBookings SET ClientPlanId = NULL WHERE BranchId = @b;
DELETE FROM AgendaClientPlans WHERE BranchId = @b;
DELETE FROM AgendaBookings WHERE BranchId = @b;
DELETE FROM AgendaClientVehicles WHERE BranchId = @b;
DELETE FROM AgendaClients WHERE BranchId = @b;
DELETE FROM AgendaLoyaltyPlans WHERE BranchId = @b;
DELETE FROM AgendaBlocks WHERE BranchId = @b;
DELETE FROM BotMessages WHERE BranchId = @b;
DELETE FROM BotSessions WHERE BranchId = @b;
DELETE FROM BotQueueItems WHERE BranchId = @b;
""")
print("limpo")

# ---------------------------------------------------------------- ajustes
cfg = call("GET", A + "/config")
weekday = (TODAY.isoweekday()) % 7  # 0 = domingo
hours = [None, [480, 1080], [480, 1080], [480, 1080], [480, 1080], [480, 1080], [480, 780]]
now_min = NOW.hour * 60 + NOW.minute
slot_now = now_min // 30 * 30
evening = now_min >= 17 * 60 or weekday in (0, 6)
if evening:
    # Depois do expediente: hoje fica aberto até perto da meia-noite para o dia ainda estar "em andamento" nos prints.
    hours[weekday] = [480, min(1410, max(1080, slot_now + 150))]
cfg.update({
    "businessName": "Brilho Total Estética Automotiva",
    "hours": hours,
    "step": 30,
    "bufferMin": 0,
    "recurringBlocks": [{"days": [1, 2, 3, 4, 5], "start": 720, "end": 780, "boxId": None, "reason": "Almoço"}],
    "holidays": {"national": False, "optional": False, "dates": []},
    "extras": [
        {"id": "x1", "name": "Cera", "active": True, "duration": {"Hatch": 30, "Sedan": 30, "SUV": 45, "Picape": 45, "Moto": 15},
         "price": {"Hatch": 40, "Sedan": 45, "SUV": 55, "Picape": 60, "Moto": 20}},
        {"id": "x2", "name": "Aromatização", "active": True, "duration": {}, "price": {"Hatch": 15, "Sedan": 15, "SUV": 15, "Picape": 15}},
    ],
    "info": {"address": "Av. das Palmeiras, 1200 - Zona Sul", "mapsUrl": "https://maps.google.com/?q=Av.+das+Palmeiras+1200",
             "payment": "Pix, dinheiro, débito e crédito em até 3x", "notes": "Estacionamento próprio para clientes."},
    "notify": {"booked": False, "ready": False, "review": False, "reviewDelayHours": 2, "waitlist": False},
    "reminder": {**(cfg.get("reminder") or {}), "sendAt": 600, "windowStart": 480, "windowEnd": 1430, "minLeadHours": 2,
                 "mondayPolicy": "saturday", "retryHours": 0},
    "planBilling": {"graceDays": 5, "suspendOverdue": False, "notifyDue": True, "notifyDaysBefore": 3, "notifyOverdue": True},
    "bot": {**(cfg.get("bot") or {}), "daysAhead": 7, "minNoticeMin": 60, "cancelMinHours": 2, "askVehicle": True,
            "showPrices": True, "maxUpcoming": 3, "noShowLimit": 2, "offerExtras": True},
})
call("PUT", A + "/config", cfg)
svc = {s["id"]: s for s in cfg["services"]}
print("ajustes salvos", "(hoje aberto até " + hm(hours[weekday][1]) + ")" if evening else "")

# ---------------------------------------------------------------- equipe
team = call("GET", A + "/equipe")
marina = next((u for u in team if u["username"] == "marina"), None)
member = {"name": "Marina Duarte", "username": "marina", "role": "staff", "active": True, "branchIds": [BID],
          "temporaryPassword": "Marina2026"}
if marina:
    call("PUT", A + f"/equipe/{marina['id']}", member)
else:
    call("POST", A + "/equipe", member)
print("equipe: marina (senha provisória Marina2026)")

# ---------------------------------------------------------------- clientes
CLIENTS = [
    # chave, nome, telefone, carros (modelo, placa, tipo, cor), vip, observações, aceita mensagens
    ("ana", "Ana Souza", "5514998110011", [("Toyota Corolla", "FHX2A31", "Sedan", "Prata")], True, "Prefere atendimento pela manhã.", True),
    ("bruno", "Bruno Lima", "5514998110012", [("Chevrolet Onix", "BRL4C22", "Hatch", "Branco")], False, "", True),
    ("carla", "Carla Mendes", "5514998110013", [("Jeep Compass", "CMD7E19", "SUV", "Preto"), ("Hyundai HB20", "HBC1D45", "Hatch", "Vermelho")],
     True, "Dois carros. O Compass tem bancos de couro.", True),
    ("diego", "Diego Rocha", "5514998110014", [("Toyota Hilux", "DGR5F60", "Picape", "Cinza")], False, "Lavagem toda sexta.", True),
    ("eduarda", "Eduarda Alves", "5514998110015", [("Renault Kwid", "EDA3G71", "Hatch", "Laranja")], False, "", True),
    ("felipe", "Felipe Martins", "5514998110016", [("Honda CG 160", "FLM8H02", "Moto", "Preta")], False, "", True),
    ("gabriela", "Gabriela Nunes", "5514998110017", [("Volkswagen Polo", "GBN2J33", "Hatch", "Azul")], False, "", True),
    ("henrique", "Henrique Costa", "5514998110018", [("Fiat Toro", "HCT6K44", "Picape", "Branca")], False, "", True),
    ("lucas", "Lucas Ferreira", "5514998110019", [("Fiat Argo", "LCF9L55", "Hatch", "Prata")], False, "", True),
    ("patricia", "Patrícia Gomes", "5514998110020", [("Nissan Kicks", "PTG4M66", "SUV", "Vermelho")], False, "Faltou uma vez.", True),
    ("rafael", "Rafael Teixeira", "5514998110021", [("BMW 320i", "RFT1N77", "Sedan", "Azul")], True, "Cliente de polimento.", True),
    ("sofia", "Sofia Ribeiro", "5514998110022", [("Honda Civic", "SFR3P88", "Sedan", "Grafite")], False, "", True),
    ("thiago", "Thiago Almeida", "5514998110023", [("Volkswagen T-Cross", "TAL5Q99", "SUV", "Branco")], False, "", True),
    ("vanessa", "Vanessa Lopes", "5514998110024", [("Jeep Renegade", "VNL7R10", "SUV", "Verde")], False, "", True),
    ("marcos", "Marcos Vieira", "5514998110025", [("Volkswagen Gol", "MVR2S21", "Hatch", "Prata")], False, "", True),
    ("rodrigo", "Rodrigo Pires", "5514998110026", [("Toyota SW4", "RDP6T32", "SUV", "Preto")], True, "", True),
    ("paulo", "Paulo Henrique", "5514998110030", [("Chevrolet Tracker", "PHT3U43", "SUV", "Cinza")], False, "", True),
]
C = {}
for key, name, phone, cars, vip, notes, opt in CLIENTS:
    c = call("POST", A + "/clientes", {
        "name": name, "phone": phone, "reminderOptIn": opt, "vip": vip, "notes": notes, "botBlocked": False,
        "vehicles": [{"vehicle": v, "plate": p, "vehicleType": t, "color": color} for v, p, t, color in cars]})
    C[key] = c
print("clientes:", len(C))

# ---------------------------------------------------------------- plano de fidelidade
plan = call("POST", A + "/planos", {"name": "Mensal Detalhada", "description": "2 lavagens detalhadas e 2 simples por mês",
                                    "discountPercent": 10, "items": [{"serviceId": "s2", "timesPerMonth": 2},
                                                                     {"serviceId": "s1", "timesPerMonth": 2}]})
PLAN_ITEMS = {"planId": plan["id"], "name": "Mensal Detalhada", "discountPercent": 10,
              "items": [{"serviceId": "s2", "timesPerMonth": 2}, {"serviceId": "s1", "timesPerMonth": 2}]}
ana_plan = call("POST", A + f"/clientes/{C['ana']['id']}/planos",
                {**PLAN_ITEMS, "vehicleId": C["ana"]["vehicles"][0]["id"], "dueDay": 10})
carla_plan = call("POST", A + f"/clientes/{C['carla']['id']}/planos",
                  {**PLAN_ITEMS, "vehicleId": C["carla"]["vehicles"][0]["id"], "dueDay": 5})
month = TODAY.strftime("%Y-%m")
call("POST", A + f"/clientes/{C['ana']['id']}/planos/{ana_plan['id']}/mensalidades/{month}/pagar", {}, ok=(200, 201, 204, 400, 409))
print("plano: Mensal Detalhada (Ana em dia, Carla em aberto)")


# ---------------------------------------------------------------- agendamentos
def booking_body(client, date, start, service, box=None, extras=None, car=0, opt=True, notes=""):
    v = client["vehicles"][car]
    return {"date": iso(date), "start": start, "boxId": box, "serviceId": service, "vehicleType": v["vehicleType"],
            "vehicle": v["vehicle"], "plate": v["plate"], "clientName": client["name"], "phone": client["phone"],
            "notes": notes, "origin": "counter", "reminderOptIn": opt, "clientId": client["id"], "vehicleId": v["id"],
            "extraIds": extras or [], "usePlan": True}


def create(client, date, start, service, box=None, **kw):
    print(f"  {client['name']}: {iso(date)} {hm(start)} {service} {box}", flush=True)
    try:
        return call("POST", A + "/agendamentos", booking_body(client, date, start, service, box, **kw))
    except RuntimeError as e:
        if box is not None and "409" in str(e):
            return call("POST", A + "/agendamentos", booking_body(client, date, start, service, None, **kw))
        raise


def status(b, s):
    return call("PATCH", A + f"/agendamentos/{b['id']}/status", {"status": s})


def pay(b, method, discount=None):
    body = {"method": method}
    if discount:
        body["discount"] = discount
    return call("POST", A + f"/agendamentos/{b['id']}/pagamento", body)


SHIFTED = []  # (booking, data real, rating)


def past(client, date, start, service, box=None, outcome="done", method="pix", discount=None, rating=None, **kw):
    """Atendimento numa data/hora que já passou: criado SHIFT_WEEKS à frente e trazido de volta no fim."""
    # Mesmo deslocamento para todos: datas reais diferentes nunca caem na mesma data futura.
    b = create(client, date + dt.timedelta(weeks=SHIFT_WEEKS), start, service, box, **kw)
    if outcome == "no_show":
        status(b, "no_show")
    elif outcome in ("done", "in_progress"):
        status(b, "in_progress")
        if outcome == "done":
            status(b, "done")
            if method:
                pay(b, method, discount)
    SHIFTED.append((b, date, rating))
    return b


# Semanas anteriores (relatórios, histórico e clientes sumidos)
def weekday_back(days):
    d = TODAY - dt.timedelta(days=days)
    while d.isoweekday() in (6, 7):  # histórico só em dias de semana (sábado fecha às 13h)
        d -= dt.timedelta(days=1)
    return d


history = [
    ("gabriela", 55, 540, "s2", "b1", "pix", None, 5), ("henrique", 95, 600, "s3", "b2", "credit", None, None),
    ("gabriela", 85, 540, "s1", "b1", "cash", None, None), ("felipe", 21, 540, "s1", "b3", "pix", None, 5),
    ("bruno", 20, 600, "s2", "b1", "debit", None, 5), ("rafael", 19, 480, "s4", "b2", "credit", 45, 5),
    ("thiago", 16, 840, "s3", "b2", "pix", None, 3), ("ana", 14, 540, "s2", "b1", None, None, 5),
    ("lucas", 13, 900, "s1", "b3", "cash", None, None), ("vanessa", 12, 540, "s6", "b1", "pix", None, 5),
    ("rodrigo", 9, 780, "s5", "b2", "credit", 90, 5), ("eduarda", 8, 600, "s1", "b3", "pix", None, 1),
    ("sofia", 7, 840, "s2", "b1", "debit", None, 5), ("marcos", 6, 540, "s1", "b3", "cash", None, None),
    ("diego", 5, 540, "s1", "b1", "pix", None, 5), ("carla", 5, 840, "s3", "b2", "credit", None, 3),
    ("patricia", 2, 900, "s2", "b1", None, None, None), ("felipe", 1, 600, "s1", "b3", "pix", None, None),
]
for key, days, start, service, box, method, discount, rating in history:
    out = "no_show" if (key == "patricia") else "done"
    past(C[key], weekday_back(days), start, service, box, outcome=out, method=method, discount=discount, rating=rating, opt=True)
print("histórico:", len(history))

# Hoje, montado em volta da hora atual
today_plan = [
    ("felipe", 480, "s1", "b3", "pix", None, None), ("rafael", 480, "s4", "b2", "credit", 45, None),
    ("lucas", 540, "s2", "b3", None, None, None), ("patricia", 570, "s1", "b1", None, None, None),
    ("sofia", 630, "s2", "b1", "cash", None, "bot"), ("thiago", 840, "s6", "b1", "pix", None, None),
    ("vanessa", 780, "s3", "b2", "debit", None, None), ("marcos", 900, "s1", "b3", "pix", None, None),
    ("rodrigo", 960, "s1", "b1", "credit", None, None),
]
if evening:
    end_today = hours[weekday][1]
    today_plan += [("eduarda", slot_now - 30, "s3", "b2", "now", None, None)]  # em atendimento agora
    if slot_now + 90 <= end_today:
        today_plan += [("bruno", slot_now + 30, "s1", "b1", "later", None, None)]
    if slot_now + 120 <= end_today:
        today_plan += [("lucas", slot_now + 60, "s1", "b3", "later", None, None)]
sofia_today = None
for key, start, service, box, method, discount, extra in today_plan:
    dur = int(svc[service]["duration"][C[key]["vehicles"][0]["vehicleType"]] or 60)
    if method == "later" or start >= now_min:
        b = past(C[key], TODAY, start, service, box, outcome="scheduled", method=None, opt=True)
    elif method == "now" or start + dur > now_min:
        b = past(C[key], TODAY, start, service, box, outcome="in_progress", method=None, opt=True)
    elif key == "patricia":
        b = past(C[key], TODAY, start, service, box, outcome="no_show", method=None, opt=True)
    else:
        if extra == "bot":
            # A Sofia avalia pelo WhatsApp: o pedido de avaliação sai na hora em que concluir.
            cfg["notify"] = {**cfg["notify"], "review": True, "reviewDelayHours": 0}
            call("PUT", A + "/config", cfg)
        b = past(C[key], TODAY, start, service, box, outcome="done", method=method, discount=discount, opt=True)
        if extra == "bot":
            sofia_today = b
            cfg["notify"] = {**cfg["notify"], "review": False}
            call("PUT", A + "/config", cfg)
print("hoje:", len(today_plan), "atendimentos")

# Traz para as datas reais (agendamento, histórico, pagamento e avaliação) e tira os lembretes da data falsa.
for b, real, rating in SHIFTED:
    fake = dt.date.fromisoformat(b["date"])
    start_local = dt.datetime.combine(real, dt.time()) + dt.timedelta(minutes=b["start"])
    end_local = dt.datetime.combine(real, dt.time()) + dt.timedelta(minutes=b["end"])
    created_utc = (start_local - dt.timedelta(days=2, hours=3) + UTC_OFFSET).strftime("%Y-%m-%d %H:%M:%S")
    end_utc = (min(end_local, NOW) + UTC_OFFSET).strftime("%Y-%m-%d %H:%M:%S")
    rate = f"Rating = {rating}, RatedAt = '{end_utc}'," if rating else ""
    sql(f"""
DECLARE @id uniqueidentifier = '{b["id"]}';
UPDATE AgendaBookings SET [Date] = '{iso(real)}', CreatedAt = '{created_utc}', UpdatedAt = '{end_utc}', {rate}
       PaidAt = CASE WHEN PaidAt IS NULL THEN NULL ELSE '{end_utc}' END WHERE Id = @id;
UPDATE AgendaAudit SET Details = REPLACE(Details, '{fake.strftime("%d/%m")} às', '{real.strftime("%d/%m")} às'),
       CreatedAt = CASE WHEN Action = 'created' THEN '{created_utc}' ELSE DATEADD(millisecond, DATEPART(second, CreatedAt) * 1000 + DATEPART(millisecond, CreatedAt), '{end_utc}') END
 WHERE BookingId = @id;
DELETE FROM AgendaReminders WHERE BookingId = @id AND Type <> 'review';
""")
print("datas ajustadas:", len(SHIFTED))

# ---------------------------------------------------------------- amanhã e a semana
tomorrow = TODAY + dt.timedelta(days=1)
while tomorrow.isoweekday() == 7:
    tomorrow += dt.timedelta(days=1)
ana = create(C["ana"], tomorrow, 540, "s2", "b1", extras=[], opt=True, notes="Pediu para caprichar nos tapetes.")
ana = call("POST", A + f"/agendamentos/{ana['id']}/remarcar", {"date": iso(tomorrow), "start": 480, "boxId": "b1"})
ana = call("PUT", A + f"/agendamentos/{ana['id']}", {"extraIds": ["x2"]})
bruno = create(C["bruno"], tomorrow, 540, "s1", "b2", extras=["x1"])
carla = create(C["carla"], tomorrow, 600, "s3", "b1", car=0)
eduarda = create(C["eduarda"], tomorrow, 840, "s4", "b2")
call("POST", A + "/bloqueios", {"boxId": "b1", "date": iso(tomorrow), "start": 840, "end": 900, "reason": "Manutenção da lavadora"})
print("amanhã:", iso(tomorrow))

# Recorrente: Diego toda sexta às 9h, 4 vezes
friday = TODAY + dt.timedelta(days=(4 - TODAY.weekday()) % 7 or 7)
call("POST", A + "/agendamentos/serie", {"booking": booking_body(C["diego"], friday, 540, "s1", "b3"), "every": "week", "count": 4})
print("série: Diego, sextas a partir de", iso(friday))

# Sábado lotado (evento) com lista de espera
saturday = TODAY + dt.timedelta(days=(5 - TODAY.weekday()) % 7 or 7)
call("POST", A + "/bloqueios", {"boxId": None, "date": iso(saturday), "start": 480, "end": 780, "reason": "Mutirão de vitrificação"})
call("POST", A + "/espera", {"date": iso(saturday), "serviceId": "s2", "vehicleType": "SUV", "clientName": "Paulo Henrique",
                             "phone": C["paulo"]["phone"]})
call("POST", A + "/espera", {"date": iso(saturday), "serviceId": "s3", "vehicleType": "Hatch", "clientName": "Larissa Campos",
                             "phone": "5514998110029", "origin": "whatsapp"})

# ---------------------------------------------------------------- lembretes de amanhã
call("POST", A + "/lembretes/processar", {})
reminders = call("GET", A + f"/lembretes?de={iso(tomorrow)}&ate={iso(tomorrow)}")
due = [r["id"] for r in reminders if r["status"] == "pending"]
if due:
    call("POST", A + "/lembretes/processar", {"ids": due})
call("POST", A + f"/agendamentos/{ana['id']}/resposta", {"resposta": "confirmed"}, ok=(200, 204, 400, 404))
call("POST", A + f"/agendamentos/{bruno['id']}/resposta", {"resposta": "confirmed"}, ok=(200, 204, 400, 404))
print("lembretes de amanhã:", len(reminders))


# ---------------------------------------------------------------- WhatsApp
TITLES = {"rate5": "Ótimo", "rate3": "Bom", "rate1": "Ruim"}  # botões do modelo (não vêm nas respostas do bot)
LAST = {}  # última lista de opções por telefone: id -> título


def bot(phone, name, text=None, opt=None):
    # Como no WhatsApp: o toque num botão chega com o id e o título que o cliente viu.
    title = LAST.get(phone, {}).get(opt) or (TITLES.get(opt.split(":")[0]) if opt else None)
    r = call("POST", A + "/bot/mensagens", {"telefone": phone, "texto": text, "opcaoId": opt, "opcaoTitulo": title, "nome": name})
    LAST[phone] = {o["id"]: o["titulo"] for m in r["mensagens"] for o in (m.get("opcoes") or [])}
    return r


def ids(r):
    return [o["id"] for o in (r["mensagens"][-1]["opcoes"] or [])] if r["mensagens"] else []


# Juliana agenda sozinha pelo WhatsApp (lavagem detalhada no HB20, depois de amanhã)
JU = ("5514998110027", "Juliana Prado")
bot(*JU, text="cancelar")
r = bot(*JU, text="Olá! Quero agendar uma lavagem")
r = bot(*JU, opt="svc:s2")
for _ in range(8):
    st, opts = r["estado"], ids(r)
    if st == "choosingVehicleType":
        r = bot(*JU, opt="vt:Hatch")
    elif st == "choosingExtras":
        r = bot(*JU, opt="ext:x2" if "ext:x2" in opts else "ext:done")
        if r["estado"] == "choosingExtras":
            r = bot(*JU, opt="ext:done")
    elif st == "choosingDay":
        days = [o for o in opts if o.startswith("day:")]
        r = bot(*JU, opt=days[1] if len(days) > 1 else days[0])
    elif st == "choosingSlot":
        slots = [o for o in opts if o.startswith("slot:")]
        r = bot(*JU, opt=slots[min(2, len(slots) - 1)])
    elif st == "askingVehicle":
        r = bot(*JU, text="HB20 JPR8V54")
    elif st == "confirming":
        r = bot(*JU, opt="confirm:ok")
        break
    else:
        break
print("bot: Juliana ->", r["estado"])

# Roberto pede atendente (fica esperando a equipe)
RO = ("5514998110028", "Roberto Dias")
bot(*RO, text="Boa noite")
bot(*RO, text="atendente")
bot(*RO, text="Vocês fazem martelinho de ouro? Tenho um amassado na porta do carro.")

# Larissa pergunta preço e a equipe responde
LA = ("5514998110029", "Larissa Campos")
bot(*LA, text="Quanto custa a higienização interna?")
call("POST", A + f"/bot/conversas/{LA[0]}/responder",
     {"texto": "Oi, Larissa! A higienização do hatch sai R$ 250 e leva 1h30. Quer que eu reserve um horário?"}, ok=(200, 204, 409))

# Sofia avalia o atendimento de hoje
if sofia_today:
    rems = call("GET", A + f"/lembretes?de={iso(TODAY)}&ate={iso(TODAY)}")
    review = [x["id"] for x in rems if x["bookingId"] == sofia_today["id"] and x["type"] == "review"]
    if review:
        call("POST", A + "/lembretes/processar", {"ids": review})
        bot("5514998110022", "Sofia Ribeiro", opt=f"rate5:{sofia_today['id']}")
        print("avaliação: Sofia deu Ótimo")

# ---------------------------------------------------------------- ajustes finais (mensagens ligadas)
cfg["notify"] = {"booked": True, "ready": True, "review": True, "reviewDelayHours": 2, "waitlist": True}
cfg["reminder"] = {**cfg["reminder"], "retryHours": 3}
call("PUT", A + "/config", cfg)
print("pronto: entre como admin e escolha", BRANCH_NAME)
