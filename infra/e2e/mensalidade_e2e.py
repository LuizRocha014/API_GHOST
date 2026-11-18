"""Teste ponta a ponta das mensalidades dos planos: vencimento, atraso, suspensão e avisos pelo WhatsApp (banco real).

Uso: python infra/e2e/mensalidade_e2e.py [http://127.0.0.1:5000]

Cria dois clientes de teste (5511990000701 e 702) com plano, um vencendo daqui a 2 dias e outro vencido há 6 (dia do
vencimento calculado a partir de hoje), liga os avisos e a suspensão nos Ajustes e devolve os originais no fim.
Com hoje até o dia 6 do mês não dá para ter mensalidade atrasada no mês: a parte do atraso é pulada.
"""
import datetime as dt
import json
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
TODAY = dt.date.today()
PHONES = ("5511990000701", "5511990000702")
results = []


def call(method, path, body=None, token=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            status, raw = r.status, r.read()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read()
    text = raw.decode("utf-8", "replace")
    try:
        return status, (json.loads(text) if text else None)
    except ValueError:
        return status, text


def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("OK   " if cond else "FAIL ") + name + (f"  [{detail}]" if detail and not cond else ""))
    return cond


s, p = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"})
if not check("login", s == 200, s):
    sys.exit(1)
tok = p["accessToken"]
s, branches = call("GET", "/api/agenda/filiais", token=tok)
A = f"/api/agenda/{branches[0]['id']}"
s, original = call("GET", A + "/config", token=tok)
DAY = (TODAY + dt.timedelta(days=1)).isoformat()


def cleanup():
    for phone in PHONES:
        s, c = call("GET", A + f"/clientes/{phone}", token=tok)
        if s == 200 and c and c.get("id"):
            s, hist = call("GET", A + f"/clientes/{c['id']}/agendamentos", token=tok)
            for b in (hist if isinstance(hist, list) else []):
                call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
            call("DELETE", A + f"/clientes/{c['id']}", token=tok)


def save_cfg(**billing):
    cfg = json.loads(json.dumps(original))
    cfg["planBilling"] = {"graceDays": 5, "suspendOverdue": False, "notifyDue": True, "notifyDaysBefore": 3, "notifyOverdue": True, **billing}
    return call("PUT", A + "/config", cfg, token=tok)[0]


def client_with_plan(phone, name, due_day):
    s, c = call("POST", A + "/clientes", {"name": name, "phone": phone, "reminderOptIn": True, "vip": False, "notes": "",
                                          "vehicles": [{"vehicle": "Gol", "plate": "", "vehicleType": "Hatch", "color": ""}]}, token=tok)
    if s != 201:
        return None, None, f"{s} {c}"
    s, plan = call("POST", A + f"/clientes/{c['id']}/planos", {
        "name": "Mensal Teste", "discountPercent": 10, "items": [{"serviceId": "s1", "timesPerMonth": 4}],
        "vehicleId": c["vehicles"][0]["id"], "dueDay": due_day}, token=tok)
    return c, plan if s == 201 else None, f"{s} {plan}"


try:
    cleanup()
    check("ajustes com avisos ligados", save_cfg() == 200)

    s, e = call("POST", A + "/clientes", {"name": "Dia Errado", "phone": "", "vehicles": []}, token=tok)
    bad_id = e.get("id") if isinstance(e, dict) else None
    s, e = call("POST", A + f"/clientes/{bad_id}/planos", {"name": "X", "discountPercent": 0, "items": [{"serviceId": "s1", "timesPerMonth": 1}],
                                                            "vehicleType": "Hatch", "dueDay": 31}, token=tok)
    check("dia de vencimento fora de 1..28 = 400", s == 400, f"{s} {e}")
    if bad_id:
        call("DELETE", A + f"/clientes/{bad_id}", token=tok)

    due_soon_day = TODAY.day + 2
    if due_soon_day <= 28:
        c1, p1, err = client_with_plan(PHONES[0], "Vera Vencendo", due_soon_day)
        check("plano com vencimento daqui a 2 dias", p1 and p1["dueDay"] == due_soon_day and p1["charges"][0]["dueDate"]
              == TODAY.replace(day=due_soon_day).isoformat() and not p1["charges"][0]["overdue"], err if not p1 else p1["charges"])
    else:
        c1 = p1 = None
        print("     (pulei o 'vencendo': hoje + 2 passa do dia 28)")

    overdue_day = TODAY.day - 6
    c2 = p2 = None
    if overdue_day >= 1:
        c2, p2, err = client_with_plan(PHONES[1], "Otávio Atrasado", overdue_day)
        check("plano vencido há 6 dias aparece em atraso", p2 and p2["charges"][0]["overdue"] and not p2["suspended"], err if not p2 else p2)
    else:
        print("     (pulei o 'atrasado': hoje é até o dia 6)")

    s, sent = call("POST", A + "/planos/avisos", token=tok)
    types = {(n["phone"], n["type"]) for n in (sent if isinstance(sent, list) else [])}
    if p1:
        check("aviso de mensalidade vencendo enviado", (PHONES[0], "due_soon") in types and all(n["status"] == "sent" for n in sent), sent)
    if p2:
        check("aviso de mensalidade atrasada enviado", (PHONES[1], "overdue") in types, sent)
    s, again = call("POST", A + "/planos/avisos", token=tok)
    check("o mesmo aviso não sai duas vezes", s == 200 and not [n for n in again if n["phone"] in PHONES], again)
    if p1:
        s, conv = call("GET", A + f"/bot/conversas/{PHONES[0]}", token=tok)
        check("aviso registrado na conversa do cliente", s == 200 and any("vence em" in m["texto"] for m in conv["mensagens"]), conv)

    if p2:
        s, b = call("POST", A + "/agendamentos", {"date": DAY, "start": 540, "boxId": None, "serviceId": "s1", "vehicleType": "Hatch",
                                                  "vehicle": "Gol", "plate": "", "clientName": c2["name"], "phone": PHONES[1], "notes": "",
                                                  "origin": "counter", "reminderOptIn": True, "extraIds": [], "clientId": c2["id"],
                                                  "vehicleId": c2["vehicles"][0]["id"]}, token=tok)
        check("sem suspensão o plano em atraso ainda cobre", s == 201 and b.get("clientPlanId") == p2["id"], f"{s} {b}")
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)

        save_cfg(suspendOverdue=True)
        s, plans = call("GET", A + f"/clientes/{c2['id']}/planos", token=tok)
        check("com suspensão ligada o plano aparece suspenso", s == 200 and plans[0]["suspended"], plans)
        s, b = call("POST", A + "/agendamentos", {"date": DAY, "start": 600, "boxId": None, "serviceId": "s1", "vehicleType": "Hatch",
                                                  "vehicle": "Gol", "plate": "", "clientName": c2["name"], "phone": PHONES[1], "notes": "",
                                                  "origin": "counter", "reminderOptIn": True, "extraIds": [], "clientId": c2["id"],
                                                  "vehicleId": c2["vehicles"][0]["id"]}, token=tok)
        check("plano suspenso não cobre: preço normal", s == 201 and b.get("clientPlanId") is None and float(b.get("price") or 0) > 0, f"{s} {b}")
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)

        month = p2["charges"][0]["month"]
        s, paid = call("POST", A + f"/clientes/{c2['id']}/planos/{p2['id']}/mensalidades/{month}/pagar", token=tok)
        check("pagou: sai do atraso e da suspensão", s == 200 and not paid["suspended"] and paid["charges"][0]["status"] == "paid", paid)
        s, b = call("POST", A + "/agendamentos", {"date": DAY, "start": 660, "boxId": None, "serviceId": "s1", "vehicleType": "Hatch",
                                                  "vehicle": "Gol", "plate": "", "clientName": c2["name"], "phone": PHONES[1], "notes": "",
                                                  "origin": "counter", "reminderOptIn": True, "extraIds": [], "clientId": c2["id"],
                                                  "vehicleId": c2["vehicles"][0]["id"]}, token=tok)
        check("depois de pagar o plano volta a cobrir", s == 201 and b.get("clientPlanId") == p2["id"], f"{s} {b}")

    save_cfg(notifyDue=False, notifyOverdue=False)
    s, none = call("POST", A + "/planos/avisos", token=tok)
    check("avisos desligados não mandam nada", s == 200 and none == [], none)

    s, tpls = call("GET", A + "/bot/modelos", token=tok)
    plan_tpls = [t for t in tpls if t["nome"].startswith("mensalidade_")]
    check("modelos da Meta para mensalidade com as variáveis", len(plan_tpls) == 2 and all("{{1}}" in t["texto"] for t in plan_tpls)
          and "vencimento" in plan_tpls[0]["variaveis"], plan_tpls)
finally:
    cleanup()
    s, _ = call("PUT", A + "/config", original, token=tok)
    check("ajustes originais devolvidos", s == 200, s)

ok = sum(1 for _, x in results if x)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
