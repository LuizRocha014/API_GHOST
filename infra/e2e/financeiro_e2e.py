"""Teste ponta a ponta do financeiro: pagamento, desconto, histórico, relatório e planilha (banco real).

Uso: python infra/e2e/financeiro_e2e.py [http://127.0.0.1:5000]

Trabalha numa quinta-feira daqui a ~9 semanas, com o telefone de teste 5511990000601, e apaga o que cria.
"""
import datetime as dt
import json
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
TODAY = dt.date.today()
DAY = (TODAY + dt.timedelta(days=63 + (3 - (TODAY + dt.timedelta(days=63)).weekday()) % 7)).isoformat()  # quinta
PHONE = "5511990000601"
results = []


def call(method, path, body=None, token=None, raw=False):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            status, body_ = r.status, r.read()
            ctype = r.headers.get("Content-Type", "")
    except urllib.error.HTTPError as e:
        status, body_, ctype = e.code, e.read(), ""
    text = body_.decode("utf-8", "replace")
    if raw:
        return status, text, ctype
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
s, cfg = call("GET", A + "/config", token=tok)
svc = {x["id"]: x for x in cfg["services"]}
P1 = float(svc["s1"]["price"]["Hatch"])


def cleanup():
    s, items = call("GET", A + f"/agendamentos?de={DAY}&ate={DAY}", token=tok)
    for b in items or []:
        call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
    s, c = call("GET", A + f"/clientes/{PHONE}", token=tok)
    if s == 200 and c and c.get("id"):
        call("DELETE", A + f"/clientes/{c['id']}", token=tok)


def book(start, phone="", name="Cliente Financeiro"):
    s, b = call("POST", A + "/agendamentos", {
        "date": DAY, "start": start, "boxId": None, "serviceId": "s1", "vehicleType": "Hatch", "vehicle": "Gol", "plate": "",
        "clientName": name, "phone": phone, "notes": "", "origin": "counter", "reminderOptIn": False, "extraIds": []}, token=tok)
    return s, b


try:
    cleanup()
    s, b1 = book(540, PHONE)
    check("agendamento sem pagamento", s == 201 and b1.get("paidAt") is None and b1.get("paymentMethod") is None, f"{s} {b1}")
    call("PATCH", A + f"/agendamentos/{b1['id']}/status", {"status": "in_progress"}, token=tok)
    call("PATCH", A + f"/agendamentos/{b1['id']}/status", {"status": "done"}, token=tok)

    s, e = call("POST", A + f"/agendamentos/{b1['id']}/pagamento", {"method": "bitcoin"}, token=tok)
    check("forma inválida = 400", s == 400, f"{s} {e}")
    s, e = call("POST", A + f"/agendamentos/{b1['id']}/pagamento", {"method": "pix", "discount": P1 + 1}, token=tok)
    check("desconto maior que o preço = 400", s == 400, f"{s} {e}")
    s, paid = call("POST", A + f"/agendamentos/{b1['id']}/pagamento", {"method": "pix", "discount": 10}, token=tok)
    check("recebido em Pix com desconto: valor = preço − desconto", s == 200 and paid["paymentMethod"] == "pix"
          and abs(float(paid["paidAmount"]) - (P1 - 10)) < 0.01 and abs(float(paid["discount"]) - 10) < 0.01 and paid["paidAt"], f"{s} {paid}")

    s, c = call("GET", A + f"/clientes/{PHONE}", token=tok)
    check("total gasto do cliente usa o valor recebido", s == 200 and abs(float(c["stats"]["totalSpent"]) - (P1 - 10)) < 0.01, c.get("stats") if isinstance(c, dict) else c)

    s, hist = call("GET", A + f"/agendamentos/{b1['id']}/historico", token=tok)
    actions = [h["action"] for h in hist or []]
    check("histórico: criado, status e recebido, com quem fez", s == 200 and actions[:1] == ["created"] and actions.count("status") == 2
          and actions[-1] == "paid" and all(h["actor"] == "Administrador" for h in hist), hist)
    check("histórico descreve a mudança de status", any("Em atendimento → Concluído" in h["details"] for h in hist or []), hist)

    s, undone = call("DELETE", A + f"/agendamentos/{b1['id']}/pagamento", token=tok)
    check("dono desfaz o pagamento", s == 200 and undone["paidAt"] is None and undone["paidAmount"] is None, f"{s} {undone}")
    s, paid = call("POST", A + f"/agendamentos/{b1['id']}/pagamento", {"method": "cash", "amount": P1 + 5}, token=tok)
    check("dinheiro com valor informado (gorjeta)", s == 200 and paid["paymentMethod"] == "cash" and abs(float(paid["paidAmount"]) - (P1 + 5)) < 0.01, f"{s} {paid}")
    s, hist = call("GET", A + f"/agendamentos/{b1['id']}/historico", token=tok)
    check("histórico guarda o desfeito", any(h["action"] == "payment_undone" for h in hist or []), hist)

    # concluído sem pagamento (a receber), falta e cancelado
    s, b2 = book(660)
    call("PATCH", A + f"/agendamentos/{b2['id']}/status", {"status": "done"}, token=tok)
    s, b3 = book(780)
    call("PATCH", A + f"/agendamentos/{b3['id']}/status", {"status": "no_show"}, token=tok)
    s, b4 = book(900)
    call("PATCH", A + f"/agendamentos/{b4['id']}/status", {"status": "canceled"}, token=tok)

    # pago adiantado e remarcado: o pagamento vai junto e o histórico junta os dois
    s, b5 = book(1020)
    s, _ = call("POST", A + f"/agendamentos/{b5['id']}/pagamento", {"method": "credit"}, token=tok)
    s, r5 = call("POST", A + f"/agendamentos/{b5['id']}/remarcar", {"date": DAY, "start": 960}, token=tok)
    check("remarcar leva o pagamento adiantado", s == 200 and r5["paymentMethod"] == "credit" and r5["paidAt"], f"{s} {r5}")
    s, hist = call("GET", A + f"/agendamentos/{r5['id']}/historico", token=tok)
    acts = [h["action"] for h in hist or []]
    check("histórico do remarcado inclui o original", acts[:2] == ["created", "paid"] and acts.count("rescheduled") == 2, acts)

    # edição: preço abaixo do desconto ajusta o desconto
    s, b6 = book(600, name="Desconto Grande")
    call("POST", A + f"/agendamentos/{b6['id']}/pagamento", {"method": "debit", "discount": P1 - 1}, token=tok)
    s, e6 = call("PUT", A + f"/agendamentos/{b6['id']}", {"price": 5}, token=tok)
    check("preço menor que o desconto: o desconto vira o preço", s == 200 and abs(float(e6.get("discount") or 0) - 5) < 0.01, f"{s} {e6}")
    s, hist = call("GET", A + f"/agendamentos/{b6['id']}/historico", token=tok)
    check("edição entra no histórico com o preço", any(h["action"] == "edited" and "preço" in h["details"] for h in hist or []), hist)

    # relatório do dia de teste
    s, rep = call("GET", A + f"/relatorios?de={DAY}&ate={DAY}", token=tok)
    ok = s == 200 and isinstance(rep, dict)
    check("relatório responde", ok, f"{s} {rep}")
    if ok:
        m, cnt = rep["money"], rep["counts"]
        expected_received = (P1 + 5) + P1 + float(e6.get("paidAmount") or 0)
        check("recebido = soma dos pagos (dinheiro + crédito + débito)", abs(float(m["received"]) - expected_received) < 0.01, m)
        check("a receber = concluído sem pagamento", abs(float(m["toReceive"]) - P1) < 0.01, m)
        check("contagem: 2 concluídos, 1 falta, 1 cancelado, 1 remarcado", cnt["done"] == 2 and cnt["noShow"] == 1
              and cnt["canceled"] == 1 and cnt["rescheduled"] == 1, cnt)
        check("taxa de falta = 1 de 3", abs(float(cnt["noShowRate"]) - 33.3) < 0.1, cnt)
        methods = {x["key"]: float(x["amount"]) for x in rep["byMethod"]}
        check("por forma de pagamento", set(methods) == {"cash", "credit", "debit"} and abs(methods["cash"] - (P1 + 5)) < 0.01, rep["byMethod"])
        check("por serviço", rep["byService"] and rep["byService"][0]["key"] == "s1" and rep["byService"][0]["count"] == 2, rep["byService"])
        check("ocupação por box", any(float(x["share"] or 0) > 0 for x in rep["byBox"]), rep["byBox"])
        check("ticket médio = recebido / pagos", abs(float(m["averageTicket"]) - round(expected_received / m["paidCount"], 2)) < 0.01, m)
        check("um dia no gráfico", len(rep["byDay"]) == 1 and rep["byDay"][0]["done"] == 2, rep["byDay"])

    s, text, ctype = call("GET", A + f"/relatorios/planilha?de={DAY}&ate={DAY}", token=tok, raw=True)
    lines = [l for l in text.splitlines() if l.strip()]
    check("planilha separada por tabulação com cabeçalho", s == 200 and "tab-separated" in ctype and lines[0].startswith("Data\tInício")
          and len(lines) >= 7 and any("Concluído" in l and "dinheiro" in l for l in lines), text[:300])

    s, e = call("GET", A + f"/relatorios?de={DAY}&ate=2000-01-01", token=tok)
    check("período invertido = 400", s == 400, f"{s} {e}")

    s, _ = call("DELETE", A + f"/agendamentos/{b2['id']}", token=tok)
    s, hist = call("GET", A + f"/agendamentos/{b2['id']}/historico", token=tok)
    check("excluído continua no histórico", s == 200 and hist and hist[-1]["action"] == "deleted", hist)
finally:
    cleanup()

ok = sum(1 for _, x in results if x)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
