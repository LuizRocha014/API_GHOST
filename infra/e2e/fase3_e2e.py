"""Teste ponta a ponta da fase 3 da agenda na API_GHOST (banco real).

Uso: python infra/e2e/fase3_e2e.py [http://127.0.0.1:5000]

Cobre: bloqueios semanais (almoço, box), feriados (nacionais, opcionais e datas próprias), folga entre atendimentos,
adicionais e serviço somado (preço, tempo, plano, remarcação), edição do agendamento, mensagens automáticas
(confirmação do balcão e carro pronto) e os adicionais no bot.

Usa a primeira filial, troca os ajustes durante o teste e devolve os originais no fim. Trabalha em segundas-feiras daqui a
~8 semanas (o bot marca nos próximos dias) e apaga os agendamentos que cria, inclusive os dos telefones de teste.
"""
import datetime as dt
import json
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
TODAY = dt.date.today()


def monday_from(d):
    return d + dt.timedelta(days=(7 - d.weekday()) % 7)


DAY = monday_from(TODAY + dt.timedelta(days=56)).isoformat()          # segunda-feira de teste
DAY2 = (dt.date.fromisoformat(DAY) + dt.timedelta(days=1)).isoformat()  # terça (feriado próprio)
DAY3 = (dt.date.fromisoformat(DAY) + dt.timedelta(days=2)).isoformat()  # quarta (edição e mensagens)
TEST_DAYS = [DAY, DAY2, DAY3]
PHONE_MSG = "5511990000301"
PHONE_BOT = "5511990000302"
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
if not check("login", s == 200 and p and p.get("accessToken"), f"{s} {p}"):
    sys.exit(1)
tok = p["accessToken"]
s, branches = call("GET", "/api/agenda/filiais", token=tok)
A = f"/api/agenda/{branches[0]['id']}"
print("     filial:", branches[0]["name"], "| dias:", ", ".join(TEST_DAYS))
s, original = call("GET", A + "/config", token=tok)
check("config original", s == 200 and original.get("services"), s)
svc = {x["id"]: x for x in original["services"]}
P1 = float(svc["s1"]["price"]["Hatch"])
D1 = int(svc["s1"]["duration"]["Hatch"])
P2 = float(svc["s2"]["price"]["Hatch"])
D2 = int(svc["s2"]["duration"]["Hatch"])
created = []


def save(**changes):
    cfg = json.loads(json.dumps(original))
    cfg["businessName"] = "Estética Teste Fase 3"
    cfg["step"] = 30
    cfg["hours"] = [None, [480, 1080], [480, 1080], [480, 1080], [480, 1080], [480, 1080], [480, 780]]
    cfg["boxes"] = [{"id": "b1", "name": "Box 1", "serviceIds": [], "color": None}]
    for x in cfg["services"]:
        x["active"] = True
    cfg["recurringBlocks"] = []
    cfg["holidays"] = {"national": False, "optional": False, "dates": []}
    cfg["bufferMin"] = 0
    cfg["extras"] = [
        {"id": "x1", "name": "Cera", "active": True, "duration": {"Hatch": 30, "Sedan": 30, "SUV": 45, "Picape": 45, "Moto": 15},
         "price": {"Hatch": 40, "Sedan": 40, "SUV": 50, "Picape": 50, "Moto": 20}},
        {"id": "x2", "name": "Aromatização", "active": True, "duration": {}, "price": {"Hatch": 15, "Sedan": 15, "SUV": 15}},
    ]
    cfg["notify"] = {"booked": False, "ready": False}
    cfg["bot"] = {**(cfg.get("bot") or {}), "daysAhead": 7, "minNoticeMin": 60, "cancelMinHours": 0, "askVehicle": True,
                  "showPrices": True, "maxUpcoming": 0, "noShowLimit": 0, "offerExtras": True}
    cfg.update(changes)
    s, _ = call("PUT", A + "/config", cfg, token=tok)
    return s


def starts(day, service="s1", vt="Hatch", extras=None):
    q = f"/disponibilidade?data={day}&servicoId={service}&tipoVeiculo={vt}&antecedenciaMin=0"
    if extras:
        q += "&adicionais=" + ",".join(extras)
    s, p = call("GET", A + q, token=tok)
    return p if s == 200 else []


def book(day, start, service="s1", phone="", extras=None, opt_in=False, name="Cliente Fase 3"):
    body = {"date": day, "start": start, "boxId": None, "serviceId": service, "vehicleType": "Hatch", "vehicle": "Gol",
            "plate": "", "clientName": name, "phone": phone, "notes": "", "origin": "counter", "reminderOptIn": opt_in,
            "extraIds": extras or []}
    s, b = call("POST", A + "/agendamentos", body, token=tok)
    if s == 201:
        created.append(b["id"])
    return s, b


def cleanup():
    for day in TEST_DAYS:
        s, items = call("GET", A + f"/agendamentos?de={day}&ate={day}", token=tok)
        for b in items or []:
            call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
    # O bot marca nos próximos dias, fora dos dias de teste: apaga o que os telefones de teste marcaram.
    s, items = call("GET", A + f"/agendamentos?de={TODAY.isoformat()}&ate={(TODAY + dt.timedelta(days=14)).isoformat()}", token=tok)
    for b in (items if s == 200 and isinstance(items, list) else []):
        if b.get("phone") in (PHONE_MSG, PHONE_BOT):
            call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
    for phone in (PHONE_MSG, PHONE_BOT):
        s, c = call("GET", A + f"/clientes/{phone}", token=tok)
        if s == 200 and c and c.get("id"):
            call("DELETE", A + f"/clientes/{c['id']}", token=tok)


try:
    cleanup()

    # ---- R1: bloqueios semanais
    check("ajustes com almoço de seg a sex", save(recurringBlocks=[
        {"days": [1, 2, 3, 4, 5], "start": 720, "end": 780, "boxId": None, "reason": "Almoço"},
        {"days": [1], "start": 900, "end": 960, "boxId": "b1", "reason": "Manutenção"},
    ]) == 200)
    st = [x["start"] for x in starts(DAY)]
    check("almoço tira 11:30, 12:00 e 12:30 (60 min)", 660 in st and 690 not in st and 720 not in st and 750 not in st and 780 in st, st)
    check("bloqueio fixo do box na segunda tira 14:30 e 15:00", 870 not in st and 900 not in st and 960 in st, st)
    st2 = [x["start"] for x in starts(DAY2)]
    check("bloqueio só de segunda não vale na terça", 900 in st2 and 720 not in st2, st2)
    s, p = book(DAY, 720)
    check("reservar no almoço = 409", s == 409, f"{s} {p}")

    # ---- R2: feriados
    check("data própria fechada", save(holidays={"national": False, "optional": False,
                                                    "dates": [{"date": DAY2, "name": "Aniversário da cidade"}]}) == 200)
    check("dia fechado não tem horário", starts(DAY2) == [] and starts(DAY) != [])
    s, p = book(DAY2, 600)
    check("reservar em dia fechado = 409", s == 409, f"{s} {p}")
    xmas = f"{TODAY.year}-12-25" if TODAY < dt.date(TODAY.year, 12, 25) else f"{TODAY.year + 1}-12-25"
    xmas_d = dt.date.fromisoformat(xmas)
    if xmas_d.weekday() < 6:
        check("Natal abre com feriados nacionais desligados", starts(xmas) != [])
        save(holidays={"national": True, "optional": False, "dates": []})
        check("Natal fecha com feriados nacionais ligados", starts(xmas) == [])
    good_friday = {2026: "2026-04-03", 2027: "2027-03-26", 2028: "2028-04-14"}
    carnival = {2027: "2027-02-09", 2028: "2028-02-29"}
    year = TODAY.year + 1
    save(holidays={"national": True, "optional": False, "dates": []})
    check("Sexta-feira Santa (Páscoa) fecha", starts(good_friday[year]) == [], good_friday[year])
    check("terça de Carnaval abre sem os opcionais", starts(carnival[year]) != [], carnival[year])
    save(holidays={"national": True, "optional": True, "dates": []})
    check("terça de Carnaval fecha com os opcionais", starts(carnival[year]) == [], carnival[year])

    # ---- R3: folga entre atendimentos
    save(bufferMin=30)
    s, b = book(DAY, 600)
    check("reserva 10:00-11:00", s == 201 and b["end"] == 600 + D1, f"{s} {b}")
    st = [x["start"] for x in starts(DAY)]
    check("folga de 30 min: 11:00 sai, 11:30 entra", 600 + D1 not in st and 600 + D1 + 30 in st, st)
    check("folga também antes: 09:00 (acaba 10:00) sai, 08:30 entra", 540 not in st and 510 in st, st)
    save(bufferMin=0)
    st = [x["start"] for x in starts(DAY)]
    check("sem folga, 11:00 volta", 600 + D1 in st, st)
    call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)

    # ---- R4: adicionais e serviço somado
    save()
    sl = starts(DAY, extras=["x1"])
    check("disponibilidade com Cera: duração + 30", sl and sl[0]["end"] - sl[0]["start"] == D1 + 30, sl[:1])
    s, b = book(DAY, 480, extras=["x1", "x2"])
    check("reserva com Cera e Aromatização", s == 201 and len(b.get("extras") or []) == 2, f"{s} {b}")
    check("preço = serviço + 40 + 15", s == 201 and abs(float(b["price"]) - (P1 + 55)) < 0.01, b.get("price"))
    check("fim = início + serviço + 30 (aromatização não soma tempo)", s == 201 and b["end"] == 480 + D1 + 30, b.get("end"))
    s, b2 = book(DAY, 840, extras=["s2"])
    item = (b2.get("extras") or [{}])[0] if s == 201 else {}
    check("segundo serviço somado", s == 201 and item.get("kind") == "service" and b2["end"] == 840 + D1 + D2
          and abs(float(b2["price"]) - (P1 + P2)) < 0.01, f"{s} {b2}")
    s, p = book(DAY, 600, extras=["zz"])
    check("adicional inexistente = 400", s == 400, f"{s} {p}")
    s, r = call("POST", A + f"/agendamentos/{b['id']}/remarcar", {"date": DAY3, "start": 600}, token=tok)
    if s == 200:
        created.append(r["id"])
    check("remarcar leva os adicionais, o tempo e o preço", s == 200 and len(r.get("extras") or []) == 2
          and r["end"] == 600 + D1 + 30 and abs(float(r["price"]) - (P1 + 55)) < 0.01, f"{s} {r}")

    # ---- R6: editar
    eid = r["id"]
    s, e = call("PUT", A + f"/agendamentos/{eid}", {"notes": "Cliente pediu capricho no painel"}, token=tok)
    check("editar observação", s == 200 and e["notes"].startswith("Cliente pediu") and len(e["extras"]) == 2, f"{s} {e}")
    s, e = call("PUT", A + f"/agendamentos/{eid}", {"extraIds": []}, token=tok)
    check("tirar os adicionais recalcula preço e fim", s == 200 and not e["extras"] and e["end"] == 600 + D1
          and abs(float(e["price"]) - P1) < 0.01, f"{s} {e}")
    s, e = call("PUT", A + f"/agendamentos/{eid}", {"serviceId": "s2", "extraIds": ["x1"]}, token=tok)
    check("trocar o serviço e pôr Cera", s == 200 and e["serviceId"] == "s2" and e["end"] == 600 + D2 + 30
          and abs(float(e["price"]) - (P2 + 40)) < 0.01, f"{s} {e}")
    s, e = call("PUT", A + f"/agendamentos/{eid}", {"price": 99.9}, token=tok)
    check("preço manual", s == 200 and abs(float(e["price"]) - 99.9) < 0.01, f"{s} {e}")
    s, e = call("PUT", A + f"/agendamentos/{eid}", {"vehicleType": "SUV", "vehicle": "Compass", "plate": "SUV1A23"}, token=tok)
    check("trocar o tipo do veículo recalcula o tempo", s == 200 and e["vehicleType"] == "SUV" and e["vehicle"] == "Compass"
          and e["plate"] == "SUV1A23" and e["end"] == 600 + int(svc["s2"]["duration"]["SUV"]) + 45, f"{s} {e}")
    s, blocker = book(DAY3, 840)
    s, e = call("PUT", A + f"/agendamentos/{eid}", {"serviceId": "s5"}, token=tok)
    check("serviço que não cabe (choca com o almoço) = 409", s == 409 and "Remarque" in (e or {}).get("mensagem", ""), f"{s} {e}")
    call("PATCH", A + f"/agendamentos/{blocker['id']}/status", {"status": "canceled"}, token=tok)
    s, e = call("PUT", A + f"/agendamentos/{blocker['id']}", {"notes": "x"}, token=tok)
    check("editar cancelado = 400", s == 400, f"{s} {e}")

    # ---- R4/R6: adicional que saiu do catálogo continua como foi marcado
    save()
    s, k1 = book(DAY2, 540, extras=["x1"])
    s_k2, k2 = book(DAY2, 900, extras=["x1"])
    check("duas reservas com Cera antes de apagar a Cera", s == 201 and s_k2 == 201, f"{s} {s_k2}")
    save(extras=[{"id": "x2", "name": "Aromatização", "active": True, "duration": {}, "price": {"Hatch": 15}}])
    s, e = call("PUT", A + f"/agendamentos/{k1['id']}", {"extraIds": ["x1", "x2"]}, token=tok)
    check("editar mantém a Cera apagada e soma a Aromatização", s == 200 and [x["name"] for x in e["extras"]] == ["Cera", "Aromatização"]
          and e["end"] == 540 + D1 + 30 and abs(float(e["price"]) - (P1 + 55)) < 0.01, f"{s} {e}")
    s, e = call("PUT", A + f"/agendamentos/{k1['id']}", {"serviceId": "s2"}, token=tok)
    check("trocar o serviço sem lista mantém a Cera apagada", s == 200 and [x["id"] for x in e["extras"]] == ["x1", "x2"]
          and e["end"] == 540 + D2 + 30, f"{s} {e}")
    call("PUT", A + f"/agendamentos/{k1['id']}", {"extraIds": ["x2"]}, token=tok)
    s, e = call("PUT", A + f"/agendamentos/{k1['id']}", {"extraIds": ["x1"]}, token=tok)
    check("depois de tirada, a Cera apagada não volta = 400", s == 400, f"{s} {e}")
    q = (f"/disponibilidade?data={DAY2}&servicoId=s1&tipoVeiculo=Hatch&antecedenciaMin=0"
         f"&ignorarAgendamentoId={k2['id']}&adicionais=x1")
    s, sl = call("GET", A + q, token=tok)
    free = [x for x in sl if x["start"] != 900] if s == 200 and isinstance(sl, list) else []
    check("remarcação com a Cera apagada tem horários (com o tempo dela)",
          free and free[-1]["end"] - free[-1]["start"] == D1 + 30, f"{s} {sl}")
    if free:
        s, r = call("POST", A + f"/agendamentos/{k2['id']}/remarcar", {"date": DAY2, "start": free[-1]["start"]}, token=tok)
        if s == 200:
            created.append(r["id"])
        check("remarcar leva a Cera apagada", s == 200 and [x["name"] for x in r["extras"]] == ["Cera"]
              and r["end"] - r["start"] == D1 + 30, f"{s} {r}")

    # ---- C1/C2: mensagens automáticas
    save(notify={"booked": True, "ready": True})
    s, m = book(DAY3, 960, phone=PHONE_MSG, opt_in=True, name="Marta Mensagem")
    s, rems = call("GET", A + f"/lembretes?de={DAY3}&ate={DAY3}", token=tok)
    mine = [x for x in rems or [] if x["bookingId"] == m["id"]]
    booked = next((x for x in mine if x["type"] == "booked"), None)
    check("agendar no balcão cria a confirmação na fila", booked and booked["status"] == "pending"
          and any(x["type"] == "confirmation_d1" for x in mine), [x["type"] for x in mine])
    s, d = call("POST", A + "/lembretes/processar", {"ids": [booked["id"]] if booked else []}, token=tok)
    check("confirmação enviada", s == 200 and d and d[0]["reminder"]["status"] == "sent", f"{s} {d}")
    s, conv = call("GET", A + f"/bot/conversas/{PHONE_MSG}", token=tok)
    tpl = [x for x in (conv or {}).get("mensagens", []) if x["tipo"] == "template"]
    check("confirmação com Remarcar e Cancelar na conversa", tpl and "está marcado" in tpl[-1]["texto"]
          and [o["titulo"] for o in tpl[-1]["opcoes"] or []] == ["Remarcar", "Cancelar"], tpl[-1:] if tpl else conv)
    call("PATCH", A + f"/agendamentos/{m['id']}/status", {"status": "in_progress"}, token=tok)
    call("PATCH", A + f"/agendamentos/{m['id']}/status", {"status": "done"}, token=tok)
    s, rems = call("GET", A + f"/lembretes?de={DAY3}&ate={DAY3}", token=tok)
    ready = next((x for x in rems or [] if x["bookingId"] == m["id"] and x["type"] == "ready"), None)
    d1 = next((x for x in rems or [] if x["bookingId"] == m["id"] and x["type"] == "confirmation_d1"), None)
    check("concluir cria o 'carro pronto' e cancela o lembrete que não saiu", ready and ready["status"] == "pending" and d1 and d1["status"] == "canceled",
          [(x["type"], x["status"]) for x in rems or [] if x["bookingId"] == m["id"]])
    s, d = call("POST", A + "/lembretes/processar", {"ids": [ready["id"]] if ready else []}, token=tok)
    check("'carro pronto' enviado", s == 200 and d and d[0]["reminder"]["status"] == "sent", f"{s} {d}")
    s, conv = call("GET", A + f"/bot/conversas/{PHONE_MSG}", token=tok)
    tpl = [x for x in (conv or {}).get("mensagens", []) if x["tipo"] == "template"]
    check("'carro pronto' na conversa", tpl and "pronto" in tpl[-1]["texto"], tpl[-1:] if tpl else conv)
    call("PATCH", A + f"/agendamentos/{m['id']}/status", {"status": "in_progress"}, token=tok)
    call("PATCH", A + f"/agendamentos/{m['id']}/status", {"status": "done"}, token=tok)
    s, rems = call("GET", A + f"/lembretes?de={DAY3}&ate={DAY3}", token=tok)
    check("concluir de novo não manda outro 'carro pronto'",
          len([x for x in rems or [] if x["bookingId"] == m["id"] and x["type"] == "ready"]) == 1)
    save(notify={"booked": False, "ready": False})
    s, m2 = book(DAY3, 480, phone=PHONE_MSG, opt_in=True, name="Marta Mensagem")
    s, rems = call("GET", A + f"/lembretes?de={DAY3}&ate={DAY3}", token=tok)
    check("com a confirmação desligada, não entra na fila",
          not [x for x in rems or [] if x["bookingId"] == m2["id"] and x["type"] == "booked"])
    s, tpls = call("GET", A + "/bot/modelos", token=tok)
    check("três modelos para a Meta", s == 200 and [t["nome"] for t in tpls] == ["lembrete_confirmacao", "agendamento_confirmado", "carro_pronto"]
          and all("{{1}}" in t["texto"] for t in tpls), [t.get("nome") for t in tpls or []])

    # ---- R4 no bot: adicionais
    save()

    def bot(text=None, opt=None):
        s, p = call("POST", A + "/bot/mensagens", {"telefone": PHONE_BOT, "texto": text, "opcaoId": opt, "nome": "Bruno Bot"}, token=tok)
        return p if s == 200 else {"estado": None, "mensagens": []}

    def ids(r):
        return [o["id"] for o in (r["mensagens"][-1]["opcoes"] if r["mensagens"] else [])]

    bot("cancelar")
    bot("oi")
    bot(opt="svc:s1")
    r = bot(opt="vt:Hatch")
    check("bot oferece os adicionais depois do veículo", r["estado"] == "choosingExtras" and ids(r) == ["ext:x1", "ext:x2", "ext:done"], ids(r))
    r = bot(opt="ext:x1")
    check("escolheu Cera: pergunta se quer mais", r["estado"] == "choosingExtras" and "Cera" in r["mensagens"][-1]["texto"]
          and ids(r) == ["ext:x2", "ext:done"], ids(r))
    r = bot("não")
    check("'não' segue para o dia", r["estado"] == "choosingDay", r["estado"])
    r = bot(opt=ids(r)[0])
    slot = next(i for i in ids(r) if i.startswith("slot:"))
    first = int(slot[5:])
    r = bot(opt=slot)
    r = bot("Gol ABC1D23")
    summary = r["mensagens"][-1]["texto"] if r["mensagens"] else ""
    check("resumo com o adicional, o tempo e o preço somados", "➕ Cera" in summary
          and f"{(first + D1 + 30) // 60:02d}:{(first + D1 + 30) % 60:02d}" in summary
          and (f"R$ {int(P1 + 40)}" in summary), summary)
    r = bot(opt="confirm:ok")
    s, c = call("GET", A + f"/clientes/{PHONE_BOT}", token=tok)
    s, hist = call("GET", A + f"/clientes/{c['id']}/agendamentos", token=tok) if c else (0, [])
    nb = (hist or [{}])[0]
    check("agendado pelo bot com a Cera", "Agendado!" in "\n".join(m["texto"] for m in r["mensagens"])
          and [x["id"] for x in nb.get("extras") or []] == ["x1"] and abs(float(nb.get("price") or 0) - (P1 + 40)) < 0.01, nb)
    save(bot={**original.get("bot", {}), "offerExtras": False, "maxUpcoming": 0, "noShowLimit": 0})
    r = bot("cancelar")
    check("'cancelar' sem fluxo mostra os horários marcados", r["estado"] == "myBookings", r["estado"])
    r = bot("oi")
    check("'oi' vendo os horários volta ao menu", r["estado"] == "choosingService", r["estado"])
    r = bot(opt="svc:s1")
    car = next((i for i in ids(r) if i.startswith("veh:") and i != "veh:other"), None)
    check("cliente do bot já tem o carro cadastrado", r["estado"] == "choosingVehicle" and car, ids(r))
    r = bot(opt=car)
    check("adicionais desligados no bot: vai direto para o dia", r["estado"] == "choosingDay", r["estado"])
    r = bot(opt="svc:s2")
    check("serviço tocado numa lista antiga recomeça com ele", r["estado"] == "choosingVehicle", r["estado"])
    bot("cancelar")
finally:
    cleanup()
    s, _ = call("PUT", A + "/config", original, token=tok)
    check("ajustes originais devolvidos", s == 200, s)

ok = sum(1 for _, x in results if x)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
