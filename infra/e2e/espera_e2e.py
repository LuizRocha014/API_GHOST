"""Teste ponta a ponta da lista de espera: bot oferece nos dias lotados, aviso quando abre vaga, botão Ver horários (banco real).

Uso: python infra/e2e/espera_e2e.py [http://127.0.0.1:5000]

Usa uma configuração de teste (1 box, só amanhã aberto nos próximos 2 dias), lota o dia, põe o cliente na lista pelo bot,
libera um horário e confere o aviso. A configuração da filial volta como estava no fim (mesmo se o teste falhar).
"""
import datetime as dt
import json
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
TODAY = dt.date.today()
DAY = (TODAY + dt.timedelta(days=1)).isoformat()
PHONE = "5511990000801"
PHONE_OTHER = "5511990000802"
FILLER = "5511990000803"
results = []


def call(method, path, body=None, token=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            status, body_ = r.status, r.read()
    except urllib.error.HTTPError as e:
        status, body_ = e.code, e.read()
    text = body_.decode("utf-8", "replace")
    try:
        return status, (json.loads(text) if text else None)
    except ValueError:
        return status, text


def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("OK   " if cond else "FAIL ") + name + (f"  [{detail}]" if detail and not cond else ""))
    return cond


s, p = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"})
if not check("login", s == 200 and p and p.get("accessToken"), f"{s} {p}"):
    sys.exit(1)
tok = p["accessToken"]
s, branches = call("GET", "/api/agenda/filiais", token=tok)
A = f"/api/agenda/{branches[0]['id']}"
s, original = call("GET", A + "/config", token=tok)
check("config original", s == 200 and original.get("services"), s)
svc = {x["id"]: x for x in original["services"]}
D1 = int(svc["s1"]["duration"]["Hatch"])


def save(**changes):
    cfg = json.loads(json.dumps(original))
    wd = (TODAY + dt.timedelta(days=1)).isoweekday() % 7  # 0 = domingo
    hours = [None] * 7
    hours[wd] = [540, 540 + 2 * D1]  # amanhã: só 2 horários de s1
    cfg["hours"] = hours
    cfg["step"] = 30
    cfg["boxes"] = [{"id": "b1", "name": "Box 1", "serviceIds": [], "color": None}]
    for x in cfg["services"]:
        x["active"] = True
    cfg["recurringBlocks"] = []
    cfg["holidays"] = {"national": False, "optional": False, "dates": []}
    cfg["bufferMin"] = 0
    cfg["bot"] = {**(cfg.get("bot") or {}), "daysAhead": 2, "minNoticeMin": 0, "cancelMinHours": 0, "askVehicle": False,
                  "maxUpcoming": 0, "noShowLimit": 0, "offerExtras": False}
    for k, v in changes.items():
        cfg[k] = {**(cfg.get(k) or {}), **v} if isinstance(v, dict) else v
    s, _ = call("PUT", A + "/config", cfg, token=tok)
    return s


def book(start, phone, name):
    s, b = call("POST", A + "/agendamentos", {
        "date": DAY, "start": start, "boxId": "b1", "serviceId": "s1", "vehicleType": "Hatch", "vehicle": "", "plate": "",
        "clientName": name, "phone": phone, "notes": "", "origin": "counter", "reminderOptIn": False, "extraIds": []}, token=tok)
    return s, b


def bot(phone, text=None, opt=None, name="Cliente Espera"):
    s, p = call("POST", A + "/bot/mensagens", {"telefone": phone, "texto": text, "opcaoId": opt, "nome": name}, token=tok)
    return p if s == 200 else {"estado": None, "mensagens": []}


def ids(r):
    return [o["id"] for o in (r["mensagens"][-1]["opcoes"] or [])] if r["mensagens"] else []


def text(r):
    return "\n".join(m["texto"] or "" for m in r["mensagens"])


RUN_START = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S")


def waitlist():
    """Pedidos do dia de teste criados nesta rodada (as rodadas anteriores deixam pedidos encerrados)."""
    s, w = call("GET", A + f"/espera?de={DAY}&ate={DAY}", token=tok)
    return [e for e in w if e["createdAt"][:19] >= RUN_START] if s == 200 and isinstance(w, list) else []


def cleanup():
    s, items = call("GET", A + f"/agendamentos?de={TODAY.isoformat()}&ate={DAY}", token=tok)
    for b in items if s == 200 and isinstance(items, list) else []:
        if b.get("phone") in (PHONE, PHONE_OTHER, FILLER):
            call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
    s, w = call("GET", A + f"/espera?de={DAY}&ate={DAY}", token=tok)
    for e in w if s == 200 and isinstance(w, list) else []:
        if e["phone"] in (PHONE, PHONE_OTHER) and e["status"] in ("waiting", "notified"):
            call("DELETE", A + f"/espera/{e['id']}", token=tok)
    for phone in (PHONE, PHONE_OTHER, FILLER):
        call("POST", A + f"/bot/conversas/{phone}/devolver", token=tok)
        s, c = call("GET", A + f"/clientes/{phone}", token=tok)
        if s == 200 and c and c.get("id"):
            call("DELETE", A + f"/clientes/{c['id']}", token=tok)


try:
    cleanup()
    check("config de teste (só amanhã, 2 horários, lista de espera desligada)", save(notify={"waitlist": False}) == 200)
    s, f1 = book(540, FILLER, "Cliente Lotado")
    s2, f2 = book(540 + D1, FILLER, "Cliente Lotado")
    check("amanhã lotado", s == 201 and s2 == 201, f"{s} {s2}")

    bot(PHONE, "cancelar")
    bot(PHONE, "oi")
    r = bot(PHONE, opt="svc:s1")
    if r["estado"] == "choosingVehicleType":
        r = bot(PHONE, opt="vt:Hatch")
    check("desligada: sem dia livre, manda falar com a equipe", "atendente" in text(r) and "nav:wait" not in ids(r), f"{r['estado']} {text(r)}")

    check("liga a lista de espera", save(notify={"waitlist": True}) == 200)
    bot(PHONE, "oi")
    r = bot(PHONE, opt="svc:s1")
    if r["estado"] == "choosingVehicleType":
        r = bot(PHONE, opt="vt:Hatch")
    check("ligada: oferece avisar se abrir vaga", ids(r) == ["nav:wait", "nav:attendant"] and "lotados" in text(r), f"{ids(r)} {text(r)}")
    r = bot(PHONE, opt="nav:wait")
    check("mostra o dia lotado", ids(r) == [f"wday:{DAY}", "nav:days"], ids(r))
    r = bot(PHONE, opt=f"wday:{DAY}")
    check("entrou na lista", r["estado"] == "idle" and "te aviso por aqui" in text(r), f"{r['estado']} {text(r)}")
    w = [e for e in waitlist() if e["phone"] == PHONE]
    check("pedido gravado: esperando, pelo WhatsApp", len(w) == 1 and w[0]["status"] == "waiting" and w[0]["origin"] == "whatsapp"
          and w[0]["serviceId"] == "s1" and w[0]["vehicleType"] == "Hatch", w)
    entry = w[0] if w else {}

    bot(PHONE, "oi")
    bot(PHONE, opt="svc:s1")
    r = bot(PHONE, opt="nav:wait") if True else None
    r = bot(PHONE, opt=f"wday:{DAY}")
    check("pedir de novo não duplica", len([e for e in waitlist() if e["phone"] == PHONE and e["status"] == "waiting"]) == 1, waitlist())

    s, e = call("POST", A + "/espera", {"date": DAY, "serviceId": "s1", "vehicleType": "Hatch", "clientName": "Outro", "phone": "123"}, token=tok)
    check("balcão: WhatsApp inválido = 400", s == 400, f"{s} {e}")
    s, other = call("POST", A + "/espera", {"date": DAY, "serviceId": "s1", "vehicleType": "Hatch", "clientName": "Outro Cliente",
                                            "phone": PHONE_OTHER}, token=tok)
    check("balcão põe outro cliente na lista", s == 200 and other["origin"] == "counter" and other["status"] == "waiting", f"{s} {other}")

    s, n = call("POST", A + "/espera/processar", token=tok)
    check("sem vaga: ninguém é avisado", s == 200 and all(e["status"] == "waiting" for e in waitlist() if e["phone"] in (PHONE, PHONE_OTHER)),
          f"{s} {n} {waitlist()}")

    call("PATCH", A + f"/agendamentos/{f2['id']}/status", {"status": "canceled"}, token=tok)
    s, n = call("POST", A + "/espera/processar", token=tok)
    now_h = dt.datetime.now().hour
    if 8 <= now_h < 21:
        w = {e["phone"]: e for e in waitlist()}
        check("abriu vaga: os dois são avisados", s == 200 and w.get(PHONE, {}).get("status") == "notified"
              and w.get(PHONE_OTHER, {}).get("status") == "notified" and w[PHONE].get("notifiedAt"), f"{s} {n} {w}")
        s, conv = call("GET", A + f"/bot/conversas/{PHONE}", token=tok)
        tpl = [x for x in (conv or {}).get("mensagens", []) if x["tipo"] == "template"]
        check("aviso com o botão Ver horários", tpl and "Abriu horário" in tpl[-1]["texto"]
              and [(o["id"], o["titulo"]) for o in tpl[-1]["opcoes"] or []] == [(f"wl:{entry.get('id')}", "Ver horários")], tpl[-1:] if tpl else conv)
        r = bot(PHONE_OTHER, opt=f"wl:{entry.get('id')}")
        check("botão de outro número não vale", "não vale mais" in text(r), text(r))
        r = bot(PHONE, opt=f"wl:{entry.get('id')}")
        slots = [i for i in ids(r) if i.startswith("slot:")]
        check("Ver horários: mostra o horário que abriu", r["estado"] == "choosingSlot" and slots == [f"slot:{540 + D1}"], f"{r['estado']} {ids(r)}")
        r = bot(PHONE, opt=slots[0] if slots else "slot:0")
        if r["estado"] == "askingVehicle":
            r = bot(PHONE, opt="veh:skip")
        r = bot(PHONE, opt="confirm:ok")
        check("agendou pelo aviso", "Agendado!" in text(r), f"{r['estado']} {text(r)}")
        call("POST", A + "/espera/processar", token=tok)
        w = {e["phone"]: e for e in waitlist()}
        check("quem agendou sai da lista (agendou)", w.get(PHONE, {}).get("status") == "booked", w.get(PHONE))
        check("o outro continua avisado", w.get(PHONE_OTHER, {}).get("status") == "notified", w.get(PHONE_OTHER))
    else:
        print("     (fora das 8h-21h: o aviso não sai; pulei as verificações de aviso)")

    s, _ = call("DELETE", A + f"/espera/{other['id']}", token=tok) if other else (0, None)
    w = {e["phone"]: e for e in waitlist()}
    check("tirar da lista", s == 204 and w.get(PHONE_OTHER, {}).get("status") == "canceled", f"{s} {w.get(PHONE_OTHER)}")

    s, tpls = call("GET", A + "/bot/modelos", token=tok)
    t = next((x for x in tpls or [] if x["nome"] == "vaga_disponivel"), None)
    check("modelo da Meta vaga_disponivel com o botão", t and t["botoes"] == ["Ver horários"] and "{{1}}" in t["texto"], t)
finally:
    s, _ = call("PUT", A + "/config", original, token=tok)
    check("config original de volta", s == 200, s)
    cleanup()

ok = sum(1 for _, c in results if c)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
