"""Teste ponta a ponta de agendamento recorrente, avaliação depois do atendimento e segundo lembrete (banco real).

Uso: python infra/e2e/avaliacao_serie_e2e.py [http://127.0.0.1:5000]

Trabalha em dias daqui a ~5 meses, com os telefones de teste 55119900007xx, e apaga o que cria. A configuração da
filial volta como estava no fim (mesmo se o teste falhar).
"""
import datetime as dt
import json
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
TODAY = dt.date.today()
PHONE_R = "5511990000702"  # avaliação
PHONE_T = "5511990000703"  # segundo lembrete
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
    except urllib.error.HTTPError as e:
        status, body_ = e.code, e.read()
    text = body_.decode("utf-8", "replace")
    if raw:
        return status, text
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


def save(**changes):
    cfg = json.loads(json.dumps(original))
    for k, v in changes.items():
        cfg[k] = {**(cfg.get(k) or {}), **v} if isinstance(v, dict) else v
    s, _ = call("PUT", A + "/config", cfg, token=tok)
    return s


def starts(day):
    s, p = call("GET", A + f"/disponibilidade?data={day}&servicoId=s1&tipoVeiculo=Hatch&antecedenciaMin=0", token=tok)
    return [x["start"] for x in p] if s == 200 and isinstance(p, list) else []


def open_day(offset, weekday, min_start=0):
    """Primeiro dia da semana pedida, a partir de hoje + offset, com horário livre depois de min_start (pula feriado)."""
    d = TODAY + dt.timedelta(days=offset)
    d += dt.timedelta(days=(weekday - d.weekday()) % 7)
    for _ in range(8):
        if [x for x in starts(d.isoformat()) if x >= min_start]:
            return d.isoformat()
        d += dt.timedelta(days=7)
    return d.isoformat()


DAY_S = open_day(140, 2)        # quarta: série semanal (4 semanas)
DAY_R = open_day(175, 1)        # terça: avaliação
DAY_T = open_day(182, 3, 780)   # quinta: segundo lembrete (horário depois das 13h)
DAYS = [DAY_S, DAY_R, DAY_T]
print("     filial:", branches[0]["name"], "| dias:", ", ".join(DAYS))


def body(day, start, phone="", opt_in=False, name="Cliente Recorrente"):
    return {"date": day, "start": start, "boxId": None, "serviceId": "s1", "vehicleType": "Hatch", "vehicle": "Gol",
            "plate": "", "clientName": name, "phone": phone, "notes": "", "origin": "counter", "reminderOptIn": opt_in,
            "extraIds": [], "usePlan": False}


def book(day, start, phone="", opt_in=False, name="Cliente Teste"):
    return call("POST", A + "/agendamentos", body(day, start, phone, opt_in, name), token=tok)


def reminders(day, booking_id):
    s, rems = call("GET", A + f"/lembretes?de={day}&ate={day}", token=tok)
    return [x for x in rems or [] if x["bookingId"] == booking_id]


def bot(phone, text=None, opt=None):
    s, p = call("POST", A + "/bot/mensagens", {"telefone": phone, "texto": text, "opcaoId": opt, "nome": "Cliente Teste"}, token=tok)
    return p if s == 200 else {"estado": None, "mensagens": []}


def last_text(r):
    return "\n".join(m["texto"] or "" for m in r["mensagens"])


def booking(bid):
    s, b = call("GET", A + f"/agendamentos/{bid}", token=tok)
    return b if s == 200 else {}


def cleanup():
    first = dt.date.fromisoformat(DAY_S)
    days = [(first + dt.timedelta(days=7 * i)).isoformat() for i in range(5)] + DAYS
    for day in sorted(set(days)):
        s, items = call("GET", A + f"/agendamentos?de={day}&ate={day}", token=tok)
        for b in items if s == 200 and isinstance(items, list) else []:
            if b.get("clientName", "").startswith("Cliente ") or b.get("phone") in (PHONE_R, PHONE_T):
                call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
    for phone in (PHONE_R, PHONE_T):
        call("POST", A + f"/bot/conversas/{phone}/devolver", token=tok)
        s, c = call("GET", A + f"/clientes/{phone}", token=tok)
        if s == 200 and c and c.get("id"):
            call("DELETE", A + f"/clientes/{c['id']}", token=tok)


try:
    cleanup()

    # ---- R9: agendamento recorrente
    st = [x for x in starts(DAY_S) if x >= 540]
    start = st[0] if st else 540
    s, e = call("POST", A + "/agendamentos/serie", {"booking": body(DAY_S, start), "every": "dia", "count": 4}, token=tok)
    check("repetição inválida = 400", s == 400, f"{s} {e}")
    s, e = call("POST", A + "/agendamentos/serie", {"booking": body(DAY_S, start), "every": "week", "count": 1}, token=tok)
    check("repetir 1 vez = 400", s == 400, f"{s} {e}")
    s, ser = call("POST", A + "/agendamentos/serie", {"booking": body(DAY_S, start, name="Cliente Recorrente"), "every": "week", "count": 4},
                  token=tok)
    items = (ser or {}).get("items") or []
    got = [x["booking"] for x in items if x.get("booking")]
    first = dt.date.fromisoformat(DAY_S)
    check("série semanal: 4 agendamentos, 7 dias entre eles", s == 200 and len(got) == 4
          and [x["date"] for x in items] == [(first + dt.timedelta(days=7 * i)).isoformat() for i in range(4)],
          f"{s} {[(x['date'], x.get('error')) for x in items]}")
    check("todos com o mesmo seriesId", got and all(b["seriesId"] == ser["seriesId"] for b in got), [b.get("seriesId") for b in got])
    if len(got) == 4:
        s, r = call("POST", A + f"/agendamentos/{got[1]['id']}/cancelar-serie", token=tok)
        check("cancelar este e os próximos: 3 cancelados", s == 200 and r and r.get("cancelados") == 3, f"{s} {r}")
        st_ = [booking(b["id"]).get("status") for b in got]
        check("o primeiro continua marcado", st_ == ["scheduled", "canceled", "canceled", "canceled"], st_)
        s, h = call("GET", A + f"/agendamentos/{got[0]['id']}/historico", token=tok)
        check("histórico do primeiro: agendado", s == 200 and any(x["action"] == "created" for x in h or []), h)
    s, single = book(DAY_S, start + 120 if start + 120 in starts(DAY_S) else (starts(DAY_S) or [600])[-1], name="Cliente Avulso")
    s, e = call("POST", A + f"/agendamentos/{single['id']}/cancelar-serie", token=tok) if single else (0, None)
    check("cancelar série de agendamento avulso = 400", s == 400, f"{s} {e}")
    s, ser2 = call("POST", A + "/agendamentos/serie", {"booking": body(DAY_S, start, name="Cliente Mensal"), "every": "month", "count": 2},
                   token=tok)
    d2 = [x["date"] for x in (ser2 or {}).get("items") or []]
    check("todo mês: mesma data no mês seguinte", s == 200 and len(d2) == 2 and d2[1][5:7] != d2[0][5:7], f"{s} {d2}")
    for x in (ser2 or {}).get("items") or []:
        if x.get("booking"):
            call("DELETE", A + f"/agendamentos/{x['booking']['id']}", token=tok)

    # ---- C3: avaliação depois do atendimento
    check("liga a avaliação (logo depois de concluir)", save(notify={"review": True, "reviewDelayHours": 0}) == 200)
    sr = [x for x in starts(DAY_R) if x >= 540]
    s, rb = book(DAY_R, sr[0] if sr else 540, phone=PHONE_R, opt_in=True, name="Cliente Avaliação")
    check("agendamento para avaliar", s == 201, f"{s} {rb}")
    call("PATCH", A + f"/agendamentos/{rb['id']}/status", {"status": "in_progress"}, token=tok)
    s, done = call("PATCH", A + f"/agendamentos/{rb['id']}/status", {"status": "done"}, token=tok)
    check("concluído sem nota", s == 200 and done.get("rating") is None, f"{s} {done}")
    rv = [x for x in reminders(DAY_R, rb["id"]) if x["type"] == "review"]
    check("concluir põe a avaliação na fila", len(rv) == 1 and rv[0]["status"] == "pending", rv)
    s, d = call("POST", A + "/lembretes/processar", {"ids": [rv[0]["id"]] if rv else []}, token=tok)
    check("avaliação enviada", s == 200 and d and d[0]["reminder"]["status"] == "sent", f"{s} {d}")
    s, conv = call("GET", A + f"/bot/conversas/{PHONE_R}", token=tok)
    tpl = [x for x in (conv or {}).get("mensagens", []) if x["tipo"] == "template"]
    opts = (tpl[-1]["opcoes"] or []) if tpl else []
    check("avaliação com Ótimo, Bom e Ruim", tpl and "Como foi o atendimento" in tpl[-1]["texto"]
          and [o["titulo"] for o in opts] == ["Ótimo", "Bom", "Ruim"]
          and [o["id"] for o in opts] == [f"rate5:{rb['id']}", f"rate3:{rb['id']}", f"rate1:{rb['id']}"], tpl[-1:] if tpl else conv)

    r = bot(PHONE_T, opt=f"rate5:{rb['id']}")
    check("outro número não avalia", "Não encontrei" in last_text(r) and booking(rb["id"]).get("rating") is None, last_text(r))
    r = bot(PHONE_R, opt=f"rate5:{rb['id']}")
    check("tocou Ótimo: agradece", r["estado"] == "idle" and "Obrigado pela avaliação" in last_text(r), f"{r['estado']} {last_text(r)}")
    check("nota 5 no agendamento", booking(rb["id"]).get("rating") == 5, booking(rb["id"]))
    s, h = call("GET", A + f"/agendamentos/{rb['id']}/historico", token=tok)
    check("histórico: cliente avaliou Ótimo", any(x["action"] == "rating" and "Ótimo" in x["details"] for x in h or []), h)
    s, rep = call("GET", A + f"/relatorios?de={DAY_R}&ate={DAY_R}", token=tok)
    rt = (rep or {}).get("ratings") or {}
    check("relatório: 1 avaliação, média 5", s == 200 and rt.get("count") == 1 and float(rt.get("average") or 0) == 5 and rt.get("great") == 1, rt)
    s, sheet = call("GET", A + f"/relatorios/planilha?de={DAY_R}&ate={DAY_R}", token=tok, raw=True)
    check("planilha com a coluna Avaliação", s == 200 and "\tAvaliação" in sheet.splitlines()[0] and sheet.rstrip().endswith("Ótimo"),
          sheet[-200:])
    r = bot(PHONE_R, opt=f"rate1:{rb['id']}")
    check("trocou para Ruim: chama a equipe", r["estado"] == "withAttendant" and "o que aconteceu" in last_text(r), f"{r['estado']} {last_text(r)}")
    check("nota corrigida para 1", booking(rb["id"]).get("rating") == 1, booking(rb["id"]))
    s, rep = call("GET", A + f"/relatorios?de={DAY_R}&ate={DAY_R}", token=tok)
    rt = (rep or {}).get("ratings") or {}
    check("relatório: ruim conta", rt.get("bad") == 1 and rt.get("great") == 0, rt)
    call("POST", A + f"/bot/conversas/{PHONE_R}/devolver", token=tok)
    call("PATCH", A + f"/agendamentos/{rb['id']}/status", {"status": "in_progress"}, token=tok)
    call("PATCH", A + f"/agendamentos/{rb['id']}/status", {"status": "done"}, token=tok)
    check("concluir de novo não pede outra avaliação", len([x for x in reminders(DAY_R, rb["id"]) if x["type"] == "review"]) == 1)
    save(notify={"review": False})
    s, rb2 = book(DAY_R, sr[1] if len(sr) > 1 else 600, phone=PHONE_R, opt_in=True, name="Cliente Avaliação")
    call("PATCH", A + f"/agendamentos/{rb2['id']}/status", {"status": "done"}, token=tok)
    check("avaliação desligada: não entra na fila", not [x for x in reminders(DAY_R, rb2["id"]) if x["type"] == "review"])
    r = bot(PHONE_R, opt=f"rate5:{rb2['id']}")
    check("dá para avaliar mesmo sem o pedido (botão antigo)", booking(rb2["id"]).get("rating") == 5, last_text(r))

    # ---- A10: segundo lembrete para quem não respondeu
    pol = original.get("reminder") or {}
    ws, we = min(pol.get("windowStart", 540), pol.get("windowEnd", 1140)), max(pol.get("windowStart", 540), pol.get("windowEnd", 1140))
    check("liga o segundo lembrete (3 h antes)", save(reminder={"retryHours": 3}) == 200)
    tt = [x for x in starts(DAY_T) if x >= 780 and ws <= x - 180 <= we]
    s, t1 = book(DAY_T, tt[0], phone=PHONE_T, opt_in=True, name="Cliente Lembrete")
    rems = reminders(DAY_T, t1["id"]) if s == 201 else []
    retry = next((x for x in rems if x["type"] == "confirmation_retry"), None)
    hm = tt[0] - 180
    check("agendar cria o lembrete da véspera e o de 3 h antes", any(x["type"] == "confirmation_d1" for x in rems) and retry
          and retry["scheduledFor"] == f"{DAY_T}T{hm // 60:02d}:{hm % 60:02d}", [(x["type"], x["scheduledFor"]) for x in rems])
    s, d = call("POST", A + "/lembretes/processar", {"ids": [retry["id"]] if retry else []}, token=tok)
    check("sem resposta: o segundo lembrete sai", s == 200 and d and d[0]["reminder"]["status"] == "sent", f"{s} {d}")
    s, conv = call("GET", A + f"/bot/conversas/{PHONE_T}", token=tok)
    tpl = [x for x in (conv or {}).get("mensagens", []) if x["tipo"] == "template"]
    check("segundo lembrete com Confirmar, Remarcar e Cancelar", tpl and [o["titulo"] for o in tpl[-1]["opcoes"] or []] == ["Confirmar", "Remarcar", "Cancelar"],
          tpl[-1:] if tpl else conv)

    s, t2 = book(DAY_T, tt[1] if len(tt) > 1 else tt[0] + 60, phone=PHONE_T, opt_in=True, name="Cliente Lembrete")
    rems = reminders(DAY_T, t2["id"]) if s == 201 else []
    d1 = next((x for x in rems if x["type"] == "confirmation_d1"), None)
    retry2 = next((x for x in rems if x["type"] == "confirmation_retry"), None)
    call("POST", A + "/lembretes/processar", {"ids": [d1["id"]] if d1 else []}, token=tok)
    r = bot(PHONE_T, opt=f"conf:{t2['id']}")
    check("cliente confirmou pelo lembrete da véspera", "confirm" in last_text(r).lower() or "até lá" in last_text(r).lower(), last_text(r))
    s, d = call("POST", A + "/lembretes/processar", {"ids": [retry2["id"]] if retry2 else []}, token=tok)
    after = next((x for x in reminders(DAY_T, t2["id"]) if x["type"] == "confirmation_retry"), {})
    check("quem respondeu não recebe o segundo", s == 200 and not d and after.get("status") == "canceled", f"{s} {d} {after}")

    r = bot(PHONE_T, opt=f"rate5:{t1['id']}")
    check("não avalia agendamento ainda marcado", "Não encontrei" in last_text(r) and booking(t1["id"]).get("rating") is None, last_text(r))

    save(reminder={"retryHours": 0})
    s, t3 = book(DAY_T, tt[2] if len(tt) > 2 else tt[0] + 120, phone=PHONE_T, opt_in=True, name="Cliente Lembrete")
    check("desligado: só o lembrete da véspera", s == 201 and [x["type"] for x in reminders(DAY_T, t3["id"])] == ["confirmation_d1"],
          [x["type"] for x in reminders(DAY_T, t3["id"])] if s == 201 else t3)

    s, tpls = call("GET", A + "/bot/modelos", token=tok)
    check("modelos para a Meta incluem a avaliação", s == 200 and "avaliacao_atendimento" in [t["nome"] for t in tpls]
          and next(t for t in tpls if t["nome"] == "avaliacao_atendimento")["botoes"] == ["Ótimo", "Bom", "Ruim"], [t.get("nome") for t in tpls or []])
finally:
    s, _ = call("PUT", A + "/config", original, token=tok)
    check("config original de volta", s == 200, s)
    cleanup()

ok = sum(1 for _, c in results if c)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
