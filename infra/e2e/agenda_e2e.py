"""Teste ponta a ponta da agenda na API_GHOST rodando com banco real.

Uso: python infra/e2e/agenda_e2e.py [http://127.0.0.1:5000] [AAAA-MM-DD]
Entra com o admin do seed, usa a primeira filial e apaga o que cria (os ajustes ficam com o nome "Estética Teste E2E").
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
