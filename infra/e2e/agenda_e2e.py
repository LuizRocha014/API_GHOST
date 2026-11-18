"""Teste ponta a ponta da agenda na API_GHOST rodando com banco real.

Uso: python infra/e2e/agenda_e2e.py [http://127.0.0.1:5000] [AAAA-MM-DD]
Entra com o admin do seed, usa a primeira filial e apaga o que cria (inclusive os clientes de teste) (os ajustes ficam com o nome "Estética Teste E2E").
Sem data, usa o primeiro dia útil a partir de depois de amanhã; o teste do mesmo telefone usa os 3 dias úteis seguintes.
"""
import datetime as dt
import json
import sys
import threading
import urllib.error
import urllib.request

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
def weekdays_from(start, n):
    out, d = [], start
    while len(out) < n:
        if d.weekday() < 5:
            out.append(d)
        d += dt.timedelta(days=1)
    return out


_day = dt.date.fromisoformat(sys.argv[2]) if len(sys.argv) > 2 else weekdays_from(dt.date.today() + dt.timedelta(days=2), 1)[0]
DAY = _day.isoformat()
OTHER_DAYS = [d.isoformat() for d in weekdays_from(_day + dt.timedelta(days=1), 3)]
RANGE = f"de={(_day - dt.timedelta(days=7)).isoformat()}&ate={(_day + dt.timedelta(days=30)).isoformat()}"
DISPATCH_UNTIL = (_day + dt.timedelta(days=1)).isoformat() + "T00:00:00"
PHONE = "11987654321"
results = []


def call(method, path, body=None, token=None, expect=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            status, raw = r.status, r.read()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read()
    text = raw.decode("utf-8", "replace")
    try:
        payload = json.loads(text) if text else None
    except ValueError:
        payload = text
    return status, payload


def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("OK   " if cond else "FAIL ") + name + (f"  [{detail}]" if detail and not cond else ""))
    return cond


# ---- auth
s, p = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"})
if not check("login admin", s == 200 and p and p.get("accessToken"), f"{s} {p}"):
    sys.exit(1)
tok = p["accessToken"]
s, p = call("POST", "/api/auth/login", {"username": "admin", "password": "errada"})
check("senha errada = 401", s == 401, s)
s, p = call("POST", "/api/auth/renovar", token=tok)
check("renovar devolve token novo", s == 200 and p.get("accessToken"), f"{s} {p}")
tok = p["accessToken"]
s, _ = call("POST", "/api/auth/renovar", token="lixo")
check("renovar com token inválido = 401", s == 401, s)

# ---- filiais
s, _ = call("GET", "/api/agenda/filiais")
check("sem token = 401", s == 401, s)
s, branches = call("GET", "/api/agenda/filiais", token=tok)
check("filiais", s == 200 and isinstance(branches, list) and branches, f"{s} {branches}")
bid = branches[0]["id"]
print("     filial:", branches[0]["name"], "/", branches[0]["companyName"], bid)
A = f"/api/agenda/{bid}"
s, p = call("GET", "/api/agenda/00000000-0000-0000-0000-000000000099/config", token=tok)
check("filial de outro = 403/404", s in (403, 404), f"{s} {p}")

# ---- config
s, cfg = call("GET", A + "/config", token=tok)
check("config padrão criada", s == 200 and cfg.get("boxes") and cfg.get("services"), f"{s} {cfg}")
cfg["businessName"] = "Estética Teste E2E"
s, cfg2 = call("PUT", A + "/config", cfg, token=tok)
check("salvar config", s == 200 and cfg2.get("businessName") == "Estética Teste E2E", f"{s} {cfg2}")
s, cfg3 = call("GET", A + "/config", token=tok)
check("config persistiu", cfg3.get("businessName") == "Estética Teste E2E")

# ---- limpa o dia de testes anteriores
s, old = call("GET", A + f"/agendamentos?de={DAY}&ate={DAY}", token=tok)
for b in old or []:
    call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
s, blocks = call("GET", A + f"/bloqueios?de={DAY}&ate={DAY}", token=tok)
for b in blocks or []:
    call("DELETE", A + f"/bloqueios/{b['id']}", token=tok)

# ---- disponibilidade
q = f"/disponibilidade?data={DAY}&servicoId=s1&tipoVeiculo=Hatch&antecedenciaMin=0"
s, slots = call("GET", A + q, token=tok)
check("disponibilidade", s == 200 and slots and slots[0]["start"] == 480, f"{s} {slots[:2] if slots else slots}")
s9 = next((x for x in slots if x["start"] == 540), None)
check("9h com 3 boxes livres", s9 and len(s9["boxIds"]) == 3, s9)

# ---- reserva concorrente no mesmo box
booking = {"date": DAY, "start": 540, "boxId": "b1", "serviceId": "s1", "vehicleType": "Hatch", "vehicle": "Gol",
           "plate": "ABC1D23", "clientName": "Ana Teste", "phone": PHONE, "notes": "", "origin": "counter",
           "reminderOptIn": True}
statuses = []
def worker():
    statuses.append(call("POST", A + "/agendamentos", booking, token=tok))
threads = [threading.Thread(target=worker) for _ in range(6)]
[t.start() for t in threads]
[t.join() for t in threads]
codes = sorted(x[0] for x in statuses)
check("6 reservas simultâneas no b1 9h: 1 vira 201, 5 viram 409", codes == [201, 409, 409, 409, 409, 409], codes)
created = next(x[1] for x in statuses if x[0] == 201)
msg409 = next((x[1] for x in statuses if x[0] == 409), None)
check("409 traz {mensagem}", isinstance(msg409, dict) and msg409.get("mensagem"), msg409)
bk = created["id"]
print("     agendamento:", bk, created.get("boxId"), created.get("start"), created.get("end"), created.get("price"))

s, slots = call("GET", A + q, token=tok)
s9 = next((x for x in slots if x["start"] == 540), None)
check("9h agora com 2 boxes livres", s9 and "b1" not in s9["boxIds"] and len(s9["boxIds"]) == 2, s9)

s, p = call("GET", A + f"/agendamentos/{bk}", token=tok)
check("buscar agendamento", s == 200 and p["clientName"] == "Ana Teste", f"{s} {p}")

# ---- validações
s, p = call("POST", A + "/agendamentos", {**booking, "start": 600, "clientName": "x" * 200}, token=tok)
check("nome longo = 400 com mensagem", s == 400 and isinstance(p, dict) and "120" in p.get("mensagem", ""), f"{s} {p}")
s, p = call("PATCH", A + f"/agendamentos/{bk}/status", {"status": "voando"}, token=tok)
check("status inválido = 400", s == 400, f"{s} {p}")
s, p = call("GET", A + f"/agendamentos/00000000-0000-0000-0000-000000000001", token=tok)
check("agendamento inexistente = 404", s == 404, f"{s} {p}")

# ---- bloqueio
s, blk = call("POST", A + "/bloqueios", {"boxId": "b2", "date": DAY, "start": 600, "end": 720, "reason": "Manutenção"}, token=tok)
check("criar bloqueio", s in (200, 201) and blk.get("id"), f"{s} {blk}")
s, p = call("POST", A + "/agendamentos", {**booking, "start": 600, "boxId": "b2", "phone": ""}, token=tok)
check("reservar em cima do bloqueio = 409", s == 409, f"{s} {p}")
s, lst = call("GET", A + f"/bloqueios?de={DAY}&ate={DAY}", token=tok)
check("listar bloqueios", s == 200 and len(lst) == 1, f"{s} {lst}")

# ---- lembrete
s, rem = call("GET", A + f"/lembretes?{RANGE}", token=tok)
mine = [r for r in rem or [] if r.get("bookingId") == bk]
check("lembrete pendente criado junto com a reserva", s == 200 and mine and mine[0]["status"] == "pending", f"{s} {mine or rem}")
s, p = call("GET", A + f"/clientes/{PHONE}", token=tok)
check("cliente criado pela reserva", s == 200 and p["reminderOptIn"], f"{s} {p}")
s, disp = call("POST", A + "/lembretes/processar", {"ate": DISPATCH_UNTIL, "ids": [mine[0]["id"]] if mine else []}, token=tok)
check("processar lembrete (envio local)", s == 200 and len(disp) == 1 and disp[0]["reminder"]["status"] == "sent", f"{s} {disp}")
wam = disp[0]["reminder"].get("wamId") if disp else None
print("     wamId local:", wam)
if wam:
    s, p = call("POST", A + "/lembretes/entrega", {"wamId": wam, "status": "delivered"}, token=tok)
    check("status de entrega", s == 204, f"{s} {p}")
s, p = call("POST", A + f"/agendamentos/{bk}/resposta", {"resposta": "confirmed"}, token=tok)
check("resposta do cliente", s in (200, 204), f"{s} {p}")
s, rem = call("GET", A + f"/lembretes?{RANGE}", token=tok)
mine = [r for r in rem if r.get("bookingId") == bk]
check("lembrete lido e confirmado", mine and mine[0]["status"] == "read" and mine[0].get("answer") == "confirmed", mine)

# ---- remarcar e status
s, nb = call("POST", A + f"/agendamentos/{bk}/remarcar", {"date": DAY, "start": 780, "boxId": "b1"}, token=tok)
check("remarcar", s == 200 and nb["start"] == 780 and nb["id"] != bk, f"{s} {nb}")
s, p = call("GET", A + f"/agendamentos/{bk}", token=tok)
check("o antigo ficou cancelado", p["status"] == "canceled", p)
nbid = nb["id"]
s, p = call("PATCH", A + f"/agendamentos/{nbid}/status", {"status": "in_progress"}, token=tok)
check("status em andamento", s == 200 and p["status"] == "in_progress", f"{s} {p}")
s, p = call("PATCH", A + f"/agendamentos/{nbid}/status", {"status": "done"}, token=tok)
check("status concluído", s == 200 and p["status"] == "done", f"{s} {p}")

# ---- clientes
s, p = call("PUT", A + f"/clientes/{PHONE}", {"phone": PHONE, "name": "Ana Souza", "reminderOptIn": False}, token=tok)
check("atualizar cliente", s == 200 and p["name"] == "Ana Souza" and not p["reminderOptIn"], f"{s} {p}")
s, p = call("GET", A + "/clientes", token=tok)
check("listar clientes", s == 200 and any(c["phone"].endswith("987654321") for c in p), f"{s} {p}")
s, p = call("GET", A + "/clientes/11900000000", token=tok)
check("cliente inexistente = 404", s == 404, f"{s} {p}")

# ---- mesmo telefone novo, dias diferentes, ao mesmo tempo
import random
phone2 = "1199" + str(random.randint(1000000, 9999999))
days = OTHER_DAYS
for d in days:
    s, old = call("GET", A + f"/agendamentos?de={d}&ate={d}", token=tok)
    for b in old or []:
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
multi = []
def worker2(d):
    multi.append(call("POST", A + "/agendamentos", {**booking, "date": d, "start": 600, "boxId": None, "phone": phone2, "clientName": "Bia"}, token=tok))
threads = [threading.Thread(target=worker2, args=(d,)) for d in days]
[t.start() for t in threads]
[t.join() for t in threads]
check("3 reservas simultâneas do mesmo telefone novo em dias diferentes", sorted(x[0] for x in multi) == [201, 201, 201], [x[0] for x in multi])
for st, b in multi:
    if st == 201:
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)

# ---- cadastro de clientes: carros, VIP e histórico
import uuid
CD = OTHER_DAYS[0]
for b in (call("GET", A + f"/agendamentos?de={CD}&ate={CD}", token=tok)[1] or []):
    call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
phone3 = "1198" + str(random.randint(1000000, 9999999))
pa, pb, pc = (f"Q{c}{random.randint(10000, 99999)}" for c in "ABC")
form = {"name": "Carlos Frota", "phone": phone3, "reminderOptIn": True, "vip": False, "notes": "",
        "vehicles": [{"vehicle": "Onix", "plate": pa, "vehicleType": "Hatch", "color": "Prata"},
                     {"vehicle": "Hilux", "plate": pb, "vehicleType": "Picape", "color": "Preta"}]}
s, cli = call("POST", A + "/clientes", form, token=tok)
check("cadastrar cliente com 2 carros", s == 201 and cli.get("id") and len(cli["vehicles"]) == 2 and cli["reminderOptIn"], f"{s} {cli}")
cid = cli["id"]
car_a = next(v for v in cli["vehicles"] if v["plate"] == pa)
car_b = next(v for v in cli["vehicles"] if v["plate"] == pb)
s, p = call("POST", A + "/clientes", {**form, "name": "Outro", "vehicles": []}, token=tok)
check("mesmo WhatsApp em outro cliente = 409", s == 409 and "Carlos Frota" in (p or {}).get("mensagem", ""), f"{s} {p}")
s, p = call("POST", A + "/clientes", {"name": "  ", "vehicles": []}, token=tok)
check("cliente sem nome = 400", s == 400, f"{s} {p}")
s, p = call("POST", A + "/clientes", {"name": "X", "vehicles": [{"vehicleType": "Hatch"}]}, token=tok)
check("carro sem modelo nem placa = 400", s == 400, f"{s} {p}")
s, p = call("POST", A + "/clientes", {"name": "X", "vehicles": [{"plate": pa, "vehicleType": "Hatch"}, {"plate": pa.lower(), "vehicleType": "SUV"}]}, token=tok)
check("placa repetida no cadastro = 400", s == 400, f"{s} {p}")
s, semzap = call("POST", A + "/clientes", {"name": "Sem Zap", "reminderOptIn": True, "vehicles": []}, token=tok)
check("cliente sem WhatsApp não fica com lembrete", s == 201 and semzap["phone"] == "" and not semzap["reminderOptIn"], f"{s} {semzap}")
s, p = call("GET", A + f"/clientes/{cid}", token=tok)
check("buscar cliente pelo id", s == 200 and p["name"] == "Carlos Frota", f"{s} {p}")
s, p = call("GET", A + f"/clientes/{uuid.uuid4()}", token=tok)
check("cliente inexistente pelo id = 404", s == 404, f"{s} {p}")

cb = {**booking, "date": CD, "start": 660, "boxId": None, "clientId": cid, "vehicleId": car_b["id"], "vehicle": "Hilux",
      "plate": pb, "vehicleType": "Picape", "clientName": "Carlos Frota", "phone": phone3}
s, b1 = call("POST", A + "/agendamentos", cb, token=tok)
check("agendar escolhendo cliente e carro", s == 201 and b1["clientId"] == cid and b1["vehicleId"] == car_b["id"], f"{s} {b1}")
s, b2 = call("POST", A + "/agendamentos", {**cb, "start": 720, "vehicleId": None, "vehicle": "Civic", "plate": pc, "vehicleType": "Sedan"}, token=tok)
check("placa nova entra nos carros do cliente", s == 201 and b2["vehicleId"] not in (None, car_a["id"], car_b["id"]), f"{s} {b2}")
s, b3 = call("POST", A + "/agendamentos", {**cb, "start": 780, "clientId": None, "vehicleId": None, "vehicle": "Onix", "plate": pa, "vehicleType": "Hatch"}, token=tok)
check("só pelo WhatsApp acha o cliente e o carro pela placa", s == 201 and b3["clientId"] == cid and b3["vehicleId"] == car_a["id"], f"{s} {b3}")
s, p = call("POST", A + "/agendamentos", {**cb, "start": 840, "vehicleId": str(uuid.uuid4())}, token=tok)
check("carro de outro cliente = 400", s == 400, f"{s} {p}")
s, p = call("POST", A + "/agendamentos", {**cb, "start": 840, "clientId": str(uuid.uuid4()), "vehicleId": None}, token=tok)
check("cliente inexistente no agendamento = 400", s == 400, f"{s} {p}")
s, p = call("GET", A + f"/clientes/{cid}", token=tok)
check("cliente agora tem 3 carros", s == 200 and sorted(v["plate"] for v in p["vehicles"]) == sorted([pa, pb, pc]), f"{s} {p}")

call("PATCH", A + f"/agendamentos/{b1['id']}/status", {"status": "in_progress"}, token=tok)
s1, _ = call("PATCH", A + f"/agendamentos/{b1['id']}/status", {"status": "done"}, token=tok)
s3, _ = call("PATCH", A + f"/agendamentos/{b3['id']}/status", {"status": "canceled"}, token=tok)
s, p = call("GET", A + f"/clientes/{cid}", token=tok)
st = (p or {}).get("stats", {})
check("histórico: 1 concluído, 1 cancelado, 1 por vir",
      s1 == 200 and s3 == 200 and st.get("done") == 1 and st.get("canceled") == 1 and st.get("upcoming") == 1, f"{s1} {s3} {st}")
check("histórico: total gasto, última visita e próximo",
      float(st.get("totalSpent", -1)) == float(b1["price"] or 0) and st.get("lastVisit") == CD and st.get("nextVisit") == f"{CD}T12:00", st)
s, hist = call("GET", A + f"/clientes/{cid}/agendamentos", token=tok)
check("agendamentos do cliente", s == 200 and [h["id"] for h in hist] == [b3["id"], b2["id"], b1["id"]], f"{s} {hist}")

car_c = next(v for v in p["vehicles"] if v["plate"] == pc)
s, p = call("PUT", A + f"/clientes/{cid}", {"name": "Carlos Frota", "phone": phone3, "reminderOptIn": True, "vip": True,
                                             "notes": "Prefere cera de carnaúba",
                                             "vehicles": [car_a, {**car_c, "color": "Branco"}]}, token=tok)
check("editar: VIP, observação e tirar um carro",
      s == 200 and p["vip"] and p["notes"].startswith("Prefere") and sorted(v["plate"] for v in p["vehicles"]) == sorted([pa, pc])
      and next(v for v in p["vehicles"] if v["plate"] == pc)["color"] == "Branco", f"{s} {p}")
s, p = call("PUT", A + f"/clientes/{cid}", {"name": "Carlos Frota", "vehicles": [{"id": str(uuid.uuid4()), "plate": "ZZZ9Z99", "vehicleType": "Hatch"}]}, token=tok)
check("editar com carro de outro cliente = 400", s == 400, f"{s} {p}")

for b in (b1, b2, b3):
    call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
s, _ = call("DELETE", A + f"/clientes/{cid}", token=tok)
s2, _ = call("GET", A + f"/clientes/{cid}", token=tok)
check("apagar cliente", s == 204 and s2 == 404, f"{s} {s2}")
s, _ = call("DELETE", A + f"/clientes/{semzap['id']}", token=tok)
check("apagar cliente sem WhatsApp", s == 204, s)
s, bia = call("GET", A + f"/clientes/{phone2}", token=tok)
if s == 200:
    call("DELETE", A + f"/clientes/{bia['id']}", token=tok)

# ---- planos de fidelidade: cadastro, adesão do cliente e uso no mês
from decimal import Decimal, ROUND_HALF_UP


def money(x):
    return Decimal(str(x)).quantize(Decimal("0.01"), rounding=ROUND_HALF_UP)


svc = {x["id"]: x for x in cfg3["services"]}
p1, p3 = money(svc["s1"]["price"]["Hatch"]), money(svc["s3"]["price"]["Hatch"])
full = p1 * 4 + p3 * 1
plan_form = {"name": "Plano Teste E2E", "description": "Lavagem toda semana + higienização no mês", "discountPercent": 15,
             "items": [{"serviceId": "s1", "timesPerMonth": 4}, {"serviceId": "s3", "timesPerMonth": 1}]}
s, plan = call("POST", A + "/planos", plan_form, token=tok)
hatch = next((x for x in (plan or {}).get("prices", []) if x["vehicleType"] == "Hatch"), {})
check("cadastrar plano", s == 201 and plan.get("id") and len(plan["items"]) == 2 and plan["activeClients"] == 0, f"{s} {plan}")
check("plano: valor cheio = preço × vezes e desconto de 15%",
      money(hatch.get("fullPrice", -1)) == full and money(hatch.get("price", -1)) == (full * Decimal("0.85")).quantize(Decimal("0.01"), ROUND_HALF_UP),
      f"{hatch} esperado {full}")
check("plano: tipo sem preço num serviço não tem valor",
      ("Moto" in svc["s3"]["price"]) or all(x["vehicleType"] != "Moto" for x in plan.get("prices", [])), plan.get("prices"))
for name, body in [("desconto acima de 100%", {**plan_form, "discountPercent": 120}),
                   ("plano sem serviço", {**plan_form, "items": []}),
                   ("0 vezes por mês", {**plan_form, "items": [{"serviceId": "s1", "timesPerMonth": 0}]}),
                   ("serviço repetido", {**plan_form, "items": [{"serviceId": "s1", "timesPerMonth": 1}, {"serviceId": "s1", "timesPerMonth": 2}]}),
                   ("serviço inexistente", {**plan_form, "items": [{"serviceId": "zz", "timesPerMonth": 1}]}),
                   ("plano sem nome", {**plan_form, "name": " "})]:
    s, p = call("POST", A + "/planos", body, token=tok)
    check(f"plano: {name} = 400", s == 400 and (p or {}).get("mensagem"), f"{s} {p}")
s, p = call("PUT", A + f"/planos/{plan['id']}", {**plan_form, "name": "Plano Teste E2E Prata", "discountPercent": 10}, token=tok)
check("editar plano", s == 200 and p["name"] == "Plano Teste E2E Prata" and float(p["discountPercent"]) == 10, f"{s} {p}")
s, p = call("PUT", A + f"/planos/{uuid.uuid4()}", plan_form, token=tok)
check("editar plano inexistente = 404", s == 404, f"{s} {p}")

# dias do teste: um dia útil daqui a ~5 semanas e o primeiro dia útil do mês seguinte a ele
PD_date = weekdays_from(_day + dt.timedelta(days=35), 1)[0]
NM_date = weekdays_from((PD_date.replace(day=1) + dt.timedelta(days=32)).replace(day=1), 1)[0]
PD, NM = PD_date.isoformat(), NM_date.isoformat()
phone4 = "1197" + str(random.randint(1000000, 9999999))
pd_plate = f"P{random.randint(100, 999)}L{random.randint(10, 99)}"
s, dora = call("POST", A + "/clientes", {"name": "Dora Plano", "phone": phone4, "reminderOptIn": False, "vehicles": [
    {"vehicle": "HB20", "plate": pd_plate, "vehicleType": "Hatch", "color": ""}]}, token=tok)
check("cliente do plano", s == 201, f"{s} {dora}")
did, dcar = dora["id"], dora["vehicles"][0]
cp_form = {"planId": plan["id"], "name": "Prata da Dora", "discountPercent": 10, "items": plan_form["items"], "vehicleId": dcar["id"]}
s, cp = call("POST", A + f"/clientes/{did}/planos", cp_form, token=tok)
check("aderir ao plano com o carro do cliente",
      s == 201 and cp["vehicleType"] == "Hatch" and money(cp["fullPrice"]) == full and money(cp["price"]) == (full * Decimal("0.9")).quantize(Decimal("0.01"), ROUND_HALF_UP)
      and cp["endDate"] is None and cp["planId"] == plan["id"], f"{s} {cp}")
s, p = call("POST", A + f"/clientes/{did}/planos", {**cp_form, "planId": None, "name": "Outro"}, token=tok)
check("segundo plano para o mesmo carro = 409", s == 409 and "Prata da Dora" in (p or {}).get("mensagem", ""), f"{s} {p}")
if "Moto" not in svc["s3"]["price"]:
    s, p = call("POST", A + f"/clientes/{did}/planos", {**cp_form, "planId": None, "vehicleId": None, "vehicleType": "Moto"}, token=tok)
    check("plano personalizado com serviço sem preço para o tipo = 400", s == 400 and "Moto" in (p or {}).get("mensagem", ""), f"{s} {p}")
s, p = call("POST", A + f"/clientes/{did}/planos", {**cp_form, "vehicleId": str(uuid.uuid4())}, token=tok)
check("plano com carro de outro cliente = 400", s == 400, f"{s} {p}")
s, p = call("GET", A + f"/clientes/{did}", token=tok)
check("cliente mostra o plano em andamento", s == 200 and p.get("planNames") == ["Prata da Dora"], f"{s} {p}")
s, p = call("GET", A + "/planos", token=tok)
mine = next((x for x in (p or []) if x["id"] == plan["id"]), {})
check("plano conta 1 cliente", mine.get("activeClients") == 1, mine)

for b in (call("GET", A + f"/agendamentos?de={PD}&ate={PD}", token=tok)[1] or []) + (call("GET", A + f"/agendamentos?de={NM}&ate={NM}", token=tok)[1] or []):
    if b.get("clientId") == did:
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
pb_base = {**booking, "date": PD, "boxId": None, "serviceId": "s3", "clientId": did, "vehicleId": dcar["id"], "vehicle": "HB20",
           "plate": pd_plate, "vehicleType": "Hatch", "clientName": "Dora Plano", "phone": phone4, "reminderOptIn": False}
s, pk1 = call("POST", A + "/agendamentos", {**pb_base, "start": 600}, token=tok)
check("agendamento coberto pelo plano (preço 0)", s == 201 and pk1["clientPlanId"] == cp["id"] and float(pk1["price"]) == 0, f"{s} {pk1}")
s, pk2 = call("POST", A + "/agendamentos", {**pb_base, "start": 780}, token=tok)
check("limite do mês atingido: cobra o preço normal",
      s == 201 and pk2["clientPlanId"] is None and money(pk2["price"]) == p3, f"{s} {pk2}")
s, plans = call("GET", A + f"/clientes/{did}/planos?data={PD}", token=tok)
use3 = next((u for u in (plans or [{}])[0].get("usage", []) if u["serviceId"] == "s3"), {})
check("uso do mês: higienização 1 de 1", s == 200 and plans[0]["month"] == PD[:7] and use3.get("used") == 1 and use3.get("timesPerMonth") == 1, f"{s} {plans}")
s, pk3 = call("POST", A + "/agendamentos", {**pb_base, "serviceId": "s1", "start": 960, "usePlan": False}, token=tok)
check("usePlan falso cobra à parte", s == 201 and pk3["clientPlanId"] is None and money(pk3["price"]) == p1, f"{s} {pk3}")

s, moved = call("POST", A + f"/agendamentos/{pk1['id']}/remarcar", {"date": NM, "start": 600}, token=tok)
check("remarcar para o mês seguinte continua no plano", s == 200 and moved["clientPlanId"] == cp["id"] and float(moved["price"]) == 0, f"{s} {moved}")
s, plans = call("GET", A + f"/clientes/{did}/planos?data={PD}", token=tok)
use3 = next((u for u in plans[0]["usage"] if u["serviceId"] == "s3"), {})
check("a vez do mês antigo voltou com a remarcação", use3.get("used") == 0, plans)
s, pk4 = call("POST", A + "/agendamentos", {**pb_base, "start": 600}, token=tok)
check("com a vez de volta, o plano cobre de novo", s == 201 and pk4["clientPlanId"] == cp["id"], f"{s} {pk4}")
s, _ = call("PATCH", A + f"/agendamentos/{pk4['id']}/status", {"status": "canceled"}, token=tok)
s, plans = call("GET", A + f"/clientes/{did}/planos?data={PD}", token=tok)
use3 = next((u for u in plans[0]["usage"] if u["serviceId"] == "s3"), {})
check("cancelar devolve a vez do mês", use3.get("used") == 0, plans)
s, pk6 = call("POST", A + "/agendamentos", {**pb_base, "start": 600}, token=tok)
call("PATCH", A + f"/agendamentos/{pk6['id']}/status", {"status": "no_show"}, token=tok)
s, plans = call("GET", A + f"/clientes/{did}/planos?data={PD}", token=tok)
use3 = next((u for u in plans[0]["usage"] if u["serviceId"] == "s3"), {})
check("falta gasta a vez do mês", pk6.get("clientPlanId") == cp["id"] and use3.get("used") == 1, plans)

month_now = dt.date.today().strftime("%Y-%m")
s, plans = call("GET", A + f"/clientes/{did}/planos", token=tok)
ch = plans[0]["charges"]
check("mensalidade do mês da adesão pendente", len(ch) == 1 and {k: ch[0][k] for k in ("month", "amount", "status", "paidAt")}
      == {"month": month_now, "amount": plans[0]["price"], "status": "pending", "paidAt": None} and ch[0]["overdue"] is False, ch)
check("plano mostra 1 agendamento futuro coberto", plans[0]["upcomingBookings"] == 1, plans[0]["upcomingBookings"])
s, p = call("GET", A + f"/clientes/{did}", token=tok)
check("cliente com 1 mensalidade pendente", p["pendingCharges"] == 1, p["pendingCharges"])
spent_before = money(p["stats"]["totalSpent"])
s, p = call("POST", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/{month_now}/pagar", token=tok)
paid = (p or {}).get("charges", [{}])[0]
check("pagar mensalidade", s == 200 and paid.get("status") == "paid" and paid.get("paidAt"), f"{s} {p}")
s, p = call("POST", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/{month_now}/pagar", token=tok)
check("pagar de novo = 409", s == 409, f"{s} {p}")
s, p = call("POST", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/2000-01/pagar", token=tok)
check("pagar mês fora do plano = 400", s == 400, f"{s} {p}")
s, p = call("POST", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/abc/pagar", token=tok)
check("mês inválido = 400", s == 400, f"{s} {p}")
s, p = call("GET", A + f"/clientes/{did}", token=tok)
check("mensalidade paga entra no total gasto e sai das pendentes",
      p["pendingCharges"] == 0 and money(p["stats"]["totalSpent"]) == spent_before + money(paid["amount"]), p["stats"])
s, p = call("DELETE", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/{month_now}", token=tok)
check("desfazer pagamento", s == 200 and p["charges"][0]["status"] == "pending", f"{s} {p}")
s, p = call("DELETE", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/{month_now}", token=tok)
check("desfazer de novo = 404", s == 404, f"{s} {p}")
call("POST", A + f"/clientes/{did}/planos/{cp['id']}/mensalidades/{month_now}/pagar", token=tok)

s, p = call("PUT", A + f"/clientes/{did}/planos/{cp['id']}", {**cp_form, "discountPercent": 20,
                                                                "items": [{"serviceId": "s1", "timesPerMonth": 2}]}, token=tok)
check("editar o plano do cliente recalcula o preço",
      s == 200 and money(p["fullPrice"]) == p1 * 2 and money(p["price"]) == (p1 * 2 * Decimal("0.8")).quantize(Decimal("0.01"), ROUND_HALF_UP), f"{s} {p}")
s, ended = call("POST", A + f"/clientes/{did}/planos/{cp['id']}/encerrar", token=tok)
check("encerrar plano do cliente", s == 200 and ended["endDate"] == dt.date.today().isoformat(), f"{s} {ended}")
s, mv = call("GET", A + f"/agendamentos/{moved['id']}", token=tok)
check("encerrar tira do plano o que estava marcado depois e volta ao preço normal",
      mv["clientPlanId"] is None and money(mv["price"]) == p3 and ended["upcomingBookings"] == 0, f"{mv} {ended['upcomingBookings']}")
check("plano encerrado mantém a mensalidade paga", ended["charges"][0]["status"] == "paid", ended["charges"])
s, p = call("POST", A + f"/clientes/{did}/planos/{cp['id']}/encerrar", token=tok)
check("encerrar de novo = 400", s == 400, f"{s} {p}")
s, p = call("GET", A + f"/clientes/{did}", token=tok)
check("plano encerrado sai da etiqueta do cliente", s == 200 and p["planNames"] == [], f"{s} {p}")
s, pk5 = call("POST", A + "/agendamentos", {**pb_base, "serviceId": "s1", "start": 600}, token=tok)
check("depois de encerrado o plano não cobre", s == 201 and pk5["clientPlanId"] is None, f"{s} {pk5}")
s, p = call("POST", A + f"/clientes/{did}/planos", {**cp_form, "planId": None, "name": "Personalizado da Dora"}, token=tok)
check("novo plano personalizado depois de encerrar", s == 201 and p["planId"] is None, f"{s} {p}")

s, _ = call("DELETE", A + f"/planos/{plan['id']}", token=tok)
s2, p = call("GET", A + "/planos", token=tok)
check("excluir plano", s == 204 and all(x["id"] != plan["id"] for x in p), f"{s} {s2}")
s, p = call("DELETE", A + f"/planos/{plan['id']}", token=tok)
check("excluir plano de novo = 404", s == 404, f"{s} {p}")
for b in (pk1, pk2, pk3, pk4, pk5, pk6, moved):
    if isinstance(b, dict) and b.get("id"):
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
s, _ = call("DELETE", A + f"/clientes/{did}", token=tok)
check("apagar cliente do plano", s == 204, s)

# ---- limpeza
s, _ = call("DELETE", A + f"/bloqueios/{blk['id']}", token=tok)
check("apagar bloqueio", s == 204, s)
s, _ = call("DELETE", A + f"/agendamentos/{nbid}", token=tok)
check("apagar agendamento", s == 204, s)
s, p = call("DELETE", A + "/agendamentos?antesDe=2000-01-01", token=tok)
check("limpeza de antigos", s == 200 and "removidos" in p, f"{s} {p}")

fails = [n for n, ok in results if not ok]
print(f"\n{len(results) - len(fails)}/{len(results)} OK")
sys.exit(1 if fails else 0)
