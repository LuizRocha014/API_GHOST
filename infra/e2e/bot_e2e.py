"""Teste ponta a ponta do bot do WhatsApp na API_GHOST rodando com banco real.

Uso: python infra/e2e/bot_e2e.py [http://127.0.0.1:5000]

Conversa pelo simulador (POST /api/agenda/{filial}/bot/mensagens), que passa pelo mesmo fluxo do webhook, e
testa o webhook de verdade (verificação, assinatura X-Hub-Signature-256, id repetido, status de entrega e botão
do template). Para a parte do webhook, a API precisa de WhatsApp:AppSecret, WhatsApp:VerifyToken e
WhatsApp:Numbers:0 (PhoneNumberId → primeira filial) nos user-secrets; o script lê de lá (ou das variáveis
WHATSAPP_APP_SECRET, WHATSAPP_VERIFY_TOKEN e WHATSAPP_PHONE_NUMBER_ID) e não imprime os valores.

Usa a primeira filial, troca os ajustes durante o teste e devolve os originais no fim. Apaga os agendamentos
e clientes dos telefones de teste (55119900001xx).
"""
import datetime as dt
import hashlib
import hmac
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TODAY = dt.date.today()
RANGE_FROM = (TODAY - dt.timedelta(days=1)).isoformat()
RANGE_TO = (TODAY + dt.timedelta(days=20)).isoformat()


def weekday_from(start):
    d = start
    while d.weekday() >= 5:
        d += dt.timedelta(days=1)
    return d


# Dia útil a partir de depois de amanhã: longe do prazo mínimo para cancelar e com lembrete D-1 no futuro.
DAY = weekday_from(TODAY + dt.timedelta(days=3)).isoformat()
DISPATCH_UNTIL = (dt.date.fromisoformat(DAY) + dt.timedelta(days=1)).isoformat() + "T00:00:00"
PHONES = {k: f"55119900001{n:02d}" for n, k in enumerate(
    ["feliz", "digitado", "duplicado", "atendente", "cancela", "conflito", "lembrete", "outro", "optout", "audio",
     "webhook", "desconhecido", "entrega", "info", "carro", "faltas", "inbox", "semjanela"], start=1)}
results = []


def call(method, path, body=None, token=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    return _send(req)


def raw(method, path, data=None, headers=None):
    req = urllib.request.Request(BASE + path, data=data, method=method)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    return _send(req)


def _send(req):
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            status, body = r.status, r.read()
    except urllib.error.HTTPError as e:
        status, body = e.code, e.read()
    text = body.decode("utf-8", "replace")
    try:
        return status, (json.loads(text) if text else None)
    except ValueError:
        return status, text


def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("OK   " if cond else "FAIL ") + name + (f"  [{detail}]" if detail and not cond else ""))
    return cond


def user_secrets():
    """Lê só as chaves do WhatsApp dos user-secrets da API (sem imprimir)."""
    out = {}
    try:
        import re
        with open(os.path.join(ROOT, "src", "Api", "Api.csproj"), encoding="utf-8") as f:
            sid = re.search(r"<UserSecretsId>([^<]+)</UserSecretsId>", f.read())
        base = os.environ.get("APPDATA") or os.path.join(os.path.expanduser("~"), ".microsoft", "usersecrets")
        path = os.path.join(base, "Microsoft", "UserSecrets", sid.group(1), "secrets.json") if os.environ.get("APPDATA") \
            else os.path.join(base, sid.group(1), "secrets.json")
        with open(path, encoding="utf-8-sig") as f:
            out = {k: v for k, v in json.load(f).items() if k.startswith("WhatsApp:")}
        if out:
            return out
    except Exception:  # noqa: BLE001
        pass
    try:
        p = subprocess.run(["dotnet", "user-secrets", "list", "--project", os.path.join(ROOT, "src", "Api")],
                           capture_output=True, text=True, encoding="utf-8", timeout=180)
        for line in p.stdout.splitlines():
            if line.startswith("WhatsApp:") and " = " in line:
                k, v = line.split(" = ", 1)
                out[k.strip()] = v.strip()
    except Exception as e:  # noqa: BLE001
        print("     (não consegui ler os user-secrets:", type(e).__name__, ")")
    return out


# ---- login e filial
s, p = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"})
if not check("login admin", s == 200 and p and p.get("accessToken"), f"{s} {p}"):
    sys.exit(1)
tok = p["accessToken"]
s, branches = call("GET", "/api/agenda/filiais", token=tok)
bid = branches[0]["id"]
A = f"/api/agenda/{bid}"
print("     filial:", branches[0]["name"], bid, "| dia do lembrete:", DAY)

created_bookings = []


def cleanup_test_data():
    s, items = call("GET", A + f"/agendamentos?de={RANGE_FROM}&ate={RANGE_TO}", token=tok)
    phones = set(PHONES.values())
    for b in items or []:
        if b["phone"] in phones or b["id"] in created_bookings or b["clientName"] in ("Balcão E2E Bot", "Carla Cadastro", "Fábio Faltas"):
            call("DELETE", A + f"/agendamentos/{b['id']}", token=tok)
    for phone in phones:
        s, c = call("GET", A + f"/clientes/{phone}", token=tok)
        if s == 200 and c and c.get("id"):
            call("DELETE", A + f"/clientes/{c['id']}", token=tok)


s, original_cfg = call("GET", A + "/config", token=tok)
check("config original lida", s == 200 and original_cfg.get("services"), s)


def save_cfg(**bot):
    cfg = json.loads(json.dumps(original_cfg))
    cfg["businessName"] = "Estética Teste Bot"
    cfg["bot"] = {**(cfg.get("bot") or {}), "daysAhead": 7, "sessionTimeoutMin": 30, "maxInvalidAttempts": 3,
                  "minNoticeMin": 60, "cancelMinHours": 0, "askVehicle": True, "showPrices": True,
                  "maxUpcoming": 3, "noShowLimit": 2, "noShowDays": 180, **bot}
    cfg["info"] = {"address": "Rua das Flores, 100 - Centro", "mapsUrl": "https://maps.app.goo.gl/exemplo",
                   "payment": "Pix, cartão e dinheiro", "notes": "Estacionamento próprio."}
    cfg["texts"] = {}
    cfg["vehicleTypes"] = {}
    for svc in cfg["services"]:
        if svc["id"] in ("s1", "s2"):
            svc["active"] = True
    s, _ = call("PUT", A + "/config", cfg, token=tok)
    return s


try:
    cleanup_test_data()
    check("ajustes de teste salvos", save_cfg() == 200)

    def bot(phone, texto=None, opcao=None, nome="Mariana Souza", mid=None, tipo=None):
        body = {"telefone": phone, "texto": texto, "opcaoId": opcao, "nome": nome, "id": mid, "tipo": tipo}
        s, p = call("POST", A + "/bot/mensagens", body, token=tok)
        if s != 200:
            print("     bot", phone, texto or opcao, "->", s, p)
            return {"estado": None, "mensagens": []}
        return p

    def last(r):
        return r["mensagens"][-1] if r["mensagens"] else {"texto": "", "opcoes": [], "tipo": None}

    def ids(r):
        return [o["id"] for o in last(r)["opcoes"]]

    sent = []

    def talk(phone, texto=None, opcao=None, **kw):
        r = bot(phone, texto, opcao, **kw)
        sent.extend(r["mensagens"])
        return r

    for phone in PHONES.values():
        bot(phone, "cancelar")  # sessão limpa (inclusive "com atendente" de uma execução anterior)

    # ---- caminho feliz
    P = PHONES["feliz"]
    r = talk(P, "oi, quero marcar uma lavagem")
    check("saudação com o nome e a lista de serviços", last(r)["tipo"] == "list" and "Mariana" in last(r)["texto"]
          and ids(r) and ids(r)[0].startswith("svc:"), last(r))
    check("menu com informações e equipe (sem 'meus agendamentos' para número novo)",
          "nav:info" in ids(r) and "nav:attendant" in ids(r) and "nav:my" not in ids(r), ids(r))
    check("estado choosingService", r["estado"] == "choosingService", r["estado"])
    r = talk(P, opcao="svc:s1")
    check("pergunta o tipo de veículo", r["estado"] == "choosingVehicleType" and "vt:Hatch" in ids(r), ids(r))
    r = talk(P, opcao="vt:Hatch")
    days = ids(r)
    check("lista de dias com horário", r["estado"] == "choosingDay" and days and days[0].startswith("day:"), days)
    r = talk(P, opcao=days[0])
    slots = ids(r)
    check("lista de horários com navegação", r["estado"] == "choosingSlot" and slots[0].startswith("slot:") and "nav:days" in slots, slots)
    chosen_day, chosen_slot = days[0][4:], int(slots[0][5:])
    r = talk(P, opcao=slots[0])
    check("pergunta modelo e placa", r["estado"] == "askingVehicle", r["estado"])
    r = talk(P, "Onix prata ABC-1D23")
    check("resumo com placa e preço", r["estado"] == "confirming" and "ABC1D23" in last(r)["texto"] and "R$" in last(r)["texto"], last(r)["texto"])
    r = talk(P, opcao="confirm:ok")
    check("agendado e volta ao início", "Agendado!" in last(r)["texto"] and r["estado"] == "idle", last(r)["texto"])
    s, day_list = call("GET", A + f"/agendamentos?de={chosen_day}&ate={chosen_day}", token=tok)
    mine = [b for b in day_list or [] if b["phone"] == P and b["status"] == "scheduled"]
    b = mine[0] if mine else {}
    check("agendamento gravado como veio do WhatsApp", len(mine) == 1 and b["origin"] == "whatsapp" and b["start"] == chosen_slot
          and b["vehicle"] == "Onix prata" and b["plate"] == "ABC1D23" and b["clientName"] == "Mariana Souza", b)

    # ---- opção digitada
    P = PHONES["digitado"]
    r = talk(P, "oi")
    svc_count = len(ids(r))
    r = talk(P, "1")
    check("número da opção escolhe o serviço", r["estado"] == "choosingVehicleType", r["estado"])
    r = talk(P, "sedan")
    check("nome digitado escolhe o tipo", r["estado"] == "choosingDay", r["estado"])
    r = talk(P, "1")
    check("número escolhe o dia", r["estado"] == "choosingSlot", r["estado"])
    typed = last(r)["opcoes"][1]["titulo"] if len(last(r)["opcoes"]) > 2 else last(r)["opcoes"][0]["titulo"]
    r = talk(P, typed.replace(":00", "h") if typed.endswith(":00") else typed)
    check(f"horário digitado ({typed})", r["estado"] == "askingVehicle", f"{r['estado']} {last(r)['texto'][:60]}")
    r = talk(P, opcao="veh:skip")
    check("pular o veículo vai para o resumo", r["estado"] == "confirming", r["estado"])
    talk(P, "cancelar")

    # ---- id repetido (reenvio do webhook)
    P = PHONES["duplicado"]
    mid = f"wamid.e2e.{uuid.uuid4().hex}"
    r1 = bot(P, "oi", mid=mid)
    r2 = bot(P, "oi", mid=mid)
    check("mensagem repetida é ignorada", r1["mensagens"] and not r2["mensagens"], f"{len(r1['mensagens'])} / {len(r2['mensagens'])}")

    # ---- entrada inválida e atendente
    P = PHONES["atendente"]
    talk(P, "oi")
    r = talk(P, "blablabla")
    check("não entendi + repete a lista", len(r["mensagens"]) == 2 and r["mensagens"][0]["tipo"] == "text"
          and "Não entendi" in r["mensagens"][0]["texto"] and r["mensagens"][1]["tipo"] == "list", r["mensagens"])
    talk(P, "???")
    r = talk(P, "não sei")
    check("3 erros oferecem atendente", ids(r) == ["nav:attendant", "nav:menu"], ids(r))
    r = talk(P, opcao="nav:attendant")
    check("com atendente", r["estado"] == "withAttendant", r["estado"])
    r = talk(P, "alguém aí?")
    check("bot fica calado com atendente", not r["mensagens"], r["mensagens"])
    r = talk(P, "menu")
    check("menu volta para o bot", r["estado"] == "choosingService", r["estado"])
    talk(P, "cancelar")

    # ---- cancelar no meio
    P = PHONES["cancela"]
    talk(P, "oi"); talk(P, opcao="svc:s1"); r = talk(P, opcao="vt:Hatch")
    r = talk(P, opcao=ids(r)[0]); talk(P, opcao=ids(r)[0]); talk(P, "Gol")
    r = talk(P, "Cancelar")
    s, all_b = call("GET", A + f"/agendamentos?de={RANGE_FROM}&ate={RANGE_TO}", token=tok)
    check("cancelar no meio não agenda nada", r["estado"] == "idle" and not [x for x in all_b if x["phone"] == P], r["estado"])

    # ---- horário ocupado antes de confirmar
    P = PHONES["conflito"]
    talk(P, "oi"); talk(P, opcao="svc:s1"); r = talk(P, opcao="vt:Hatch")
    day_id = ids(r)[0]
    r = talk(P, opcao=day_id)
    slot_id = ids(r)[0]
    talk(P, opcao=slot_id)
    r = talk(P, "Gol XYZ1A23")
    check("conflito: chegou ao resumo", r["estado"] == "confirming", r["estado"])
    # Ocupa todos os boxes do horário escolhido pelo balcão antes do cliente confirmar.
    start = int(slot_id[5:])
    for _ in range(10):
        s, nb = call("POST", A + "/agendamentos", {"date": day_id[4:], "start": start, "boxId": None, "serviceId": "s1",
                                                  "vehicleType": "Hatch", "vehicle": "Gol", "plate": "", "clientName": "Balcão E2E Bot",
                                                  "phone": "", "notes": "", "origin": "counter", "reminderOptIn": False}, token=tok)
        if s != 201:
            break
        created_bookings.append(nb["id"])
    r = talk(P, opcao="confirm:ok")
    check("horário ocupado: avisa e reenvia a lista", "acabou de ser ocupado" in " ".join(m["texto"] for m in r["mensagens"])
          and r["estado"] in ("choosingSlot", "choosingDay") and slot_id not in ids(r), f"{r['estado']} {ids(r)[:3]}")
    talk(P, "cancelar")

    # ---- respostas do lembrete (dono, nono dígito, prazo mínimo, remarcar)
    P = PHONES["lembrete"]
    base = {"date": DAY, "start": 600, "boxId": None, "serviceId": "s1", "vehicleType": "SUV", "vehicle": "Compass",
            "plate": "QWE4F55", "clientName": "Mariana Souza", "phone": P, "notes": "", "origin": "counter", "reminderOptIn": False}
    s, bk = call("POST", A + "/agendamentos", base, token=tok)
    check("agendamento do balcão para o lembrete", s == 201, f"{s} {bk}")
    r = talk(P, opcao=f"conf:{bk['id']}")
    check("confirmar pelo lembrete", "Confirmado" in last(r)["texto"], last(r)["texto"])
    r = talk(PHONES["outro"], opcao=f"canc:{bk['id']}")
    s, same = call("GET", A + f"/agendamentos/{bk['id']}", token=tok)
    check("outro número não cancela", "Não encontrei" in last(r)["texto"] and same["status"] == "scheduled", last(r)["texto"])
    no9 = P[:4] + P[5:]
    r = talk(no9, opcao=f"conf:{bk['id']}")
    check("wa_id sem o nono dígito é o mesmo dono", "Confirmado" in last(r)["texto"], last(r)["texto"])
    save_cfg(cancelMinHours=24 * 30)
    r = talk(P, opcao=f"canc:{bk['id']}")
    s, same = call("GET", A + f"/agendamentos/{bk['id']}", token=tok)
    check("dentro do prazo mínimo vai para a equipe", "Faltam menos de" in last(r)["texto"] and ids(r) == ["nav:attendant"]
          and same["status"] == "scheduled", last(r)["texto"])
    save_cfg()
    r = talk(P, opcao=f"canc:{bk['id']}")
    s, same = call("GET", A + f"/agendamentos/{bk['id']}", token=tok)
    check("cancelar pelo lembrete libera o horário", "liberado" in last(r)["texto"] and same["status"] == "canceled", last(r)["texto"])
    r = talk(P, opcao=f"canc:{bk['id']}")
    check("cancelado de novo: avisa o status", "cancelado" in last(r)["texto"].lower(), last(r)["texto"])
    s, bk2 = call("POST", A + "/agendamentos", base, token=tok)
    r = talk(P, opcao=f"rem:{bk2['id']}")
    s, same = call("GET", A + f"/agendamentos/{bk2['id']}", token=tok)
    check("remarcar: antigo continua reservado", r["estado"] == "choosingDay" and same["status"] == "scheduled", r["estado"])
    r = talk(P, opcao=f"day:{DAY}")
    new_slot = next((i for i in ids(r) if i.startswith("slot:") and i != "slot:600"), None)
    r = talk(P, opcao=new_slot)
    check("remarcar não pergunta o veículo de novo", r["estado"] == "confirming" and "Novo horário" in last(r)["texto"], r["estado"])
    r = talk(P, opcao="confirm:ok")
    s, day_list = call("GET", A + f"/agendamentos?de={DAY}&ate={DAY}", token=tok)
    old = next((x for x in day_list if x["id"] == bk2["id"]), {})
    new = next((x for x in day_list if x.get("rescheduledFrom") == bk2["id"]), {})
    check("remarcado: antigo cancelado, novo com o mesmo veículo", "Remarcado" in last(r)["texto"] and old.get("status") == "canceled"
          and new.get("start") == int(new_slot[5:]) and new.get("vehicle") == "Compass" and new.get("vehicleType") == "SUV",
          f"{last(r)['texto'][:40]} {old.get('status')} {new}")

    # ---- parar e voltar lembretes
    P = PHONES["optout"]
    r = talk(P, "parar lembretes")
    s, c = call("GET", A + f"/clientes/{P}", token=tok)
    check("parar lembretes grava o opt-out", "não vai mais receber" in last(r)["texto"] and s == 200 and c["reminderOptIn"] is False, f"{s} {c}")
    r = talk(P, "quero lembretes")
    s, c = call("GET", A + f"/clientes/{P}", token=tok)
    check("quero lembretes volta o opt-in", c["reminderOptIn"] is True, c)

    # ---- tipo que o bot não entende
    r = talk(PHONES["audio"], tipo="audio")
    check("áudio: só entendo texto", "só entendo texto" in last(r)["texto"], last(r)["texto"])

    # ================= Fase 2 =================

    def texts(r):
        return "\n".join(m["texto"] for m in r["mensagens"])

    # ---- A7: informações da estética
    P = PHONES["info"]
    r = talk(P, "quanto custa a lavagem simples?")
    check("pergunta de preço no início: informações e depois o menu",
          len(r["mensagens"]) == 2 and "Preços" in r["mensagens"][0]["texto"] and "Rua das Flores" in r["mensagens"][0]["texto"]
          and "Pix" in r["mensagens"][0]["texto"] and r["mensagens"][1]["tipo"] == "list" and r["estado"] == "choosingService",
          [m["texto"][:60] for m in r["mensagens"]])
    r = talk(P, "endereço")
    check("comando endereço: informações + Agendar/Falar com a equipe",
          "Horário de funcionamento" in texts(r) and ids(r) == ["nav:menu", "nav:attendant"] and r["estado"] == "idle", texts(r)[:200])
    r = talk(P, opcao="nav:info")
    check("opção do menu 'Endereço e horários'", "Estacionamento próprio" in texts(r), texts(r)[:120])

    # ---- A3/A4: cliente cadastrado com carros e plano de fidelidade
    P = PHONES["carro"]
    s, carla = call("POST", A + "/clientes", {"name": "Carla Cadastro", "phone": P, "reminderOptIn": False, "vip": False, "notes": "",
                                               "vehicles": [{"vehicle": "Civic", "plate": "CAR1A11", "vehicleType": "Sedan", "color": ""},
                                                            {"vehicle": "Biz", "plate": "MOT2B22", "vehicleType": "Moto", "color": ""}]},
                     token=tok)
    check("cliente cadastrado com 2 veículos", s == 201, f"{s} {carla}")
    cid = carla["id"]
    civic = next(v for v in carla["vehicles"] if v["plate"] == "CAR1A11")
    s, plan = call("POST", A + f"/clientes/{cid}/planos", {"planId": None, "name": "Plano Bot", "discountPercent": 0,
                                                           "items": [{"serviceId": "s1", "timesPerMonth": 2}], "vehicleId": civic["id"]},
                   token=tok)
    check("plano do Civic: 2 lavagens simples por mês", s == 201, f"{s} {plan}")
    r = talk(P, "oi")
    check("saudação usa o nome do cadastro (não o do perfil)", "Carla" in last(r)["texto"] and "Mariana" not in last(r)["texto"], last(r)["texto"][:80])
    made = []
    for k in range(3):
        r = talk(P, opcao="svc:s1")
        if k == 0:
            check("oferece os veículos cadastrados + outro", r["estado"] == "choosingVehicle"
                  and f"veh:{civic['id']}" in ids(r) and "veh:other" in ids(r) and len(ids(r)) == 3, ids(r))
        r = talk(P, opcao=f"veh:{civic['id']}")
        if k == 0:
            check("carro escolhido pula o tipo de veículo", r["estado"] == "choosingDay" and "Civic" in last(r)["texto"], last(r)["texto"][:80])
        days = ids(r)
        r = talk(P, opcao=days[0])
        slot_ids = [i for i in ids(r) if i.startswith("slot:")]
        r = talk(P, opcao=slot_ids[min(k, len(slot_ids) - 1)])
        summary = last(r)["texto"]
        if k == 0:
            check("não pergunta modelo e placa de carro cadastrado", r["estado"] == "confirming" and "CAR1A11" in summary, summary)
            check("resumo: incluso no plano (1º de 2)", "Incluso no seu plano *Plano Bot* (1º de 2" in summary and "💰" not in summary, summary)
        if k == 1:
            check("resumo: incluso no plano (2º de 2)", "(2º de 2" in summary, summary)
        if k == 2:
            check("resumo: plano esgotado no mês → preço normal", "já usou as 2 vezes" in summary and "💰" in summary, summary)
        r = talk(P, opcao="confirm:ok")
        if k == 0:
            check("agendado com aviso do plano", "Agendado!" in texts(r) and "Incluso no seu plano." in texts(r), texts(r))
        made.append(days[0][4:])
    s, all_b = call("GET", A + f"/agendamentos?de={RANGE_FROM}&ate={RANGE_TO}", token=tok)
    mine = [b for b in all_b if b["phone"] == P and b["status"] == "scheduled"]
    check("3 agendamentos ligados ao cliente e ao carro, 2 pelo plano",
          len(mine) == 3 and all(b["clientId"] == cid and b["vehicleId"] == civic["id"] and b["clientName"] == "Carla Cadastro"
                                 and b["vehicleType"] == "Sedan" for b in mine)
          and sum(1 for b in mine if b["clientPlanId"]) == 2 and sum(1 for b in mine if b["clientPlanId"] and float(b["price"] or 0) == 0) == 2,
          [(b["start"], b["clientPlanId"], b["price"]) for b in mine])
    s, c = call("GET", A + f"/clientes/{cid}", token=tok)
    check("nenhum carro novo no cadastro", s == 200 and len(c["vehicles"]) == 2, c and c.get("vehicles"))

    # ---- A8: limite de horários futuros
    r = talk(P, "oi")
    check("menu mostra 'Meus agendamentos' para quem tem horário", "nav:my" in ids(r), ids(r))
    r = talk(P, opcao="svc:s1")
    check("limite de 3 horários: recusa e lista os horários", "o limite por aqui é 3" in texts(r) and r["estado"] == "myBookings"
          and len([i for i in ids(r) if i.startswith("bk:")]) == 3 and "nav:menu" in ids(r), texts(r)[:120])

    # ---- A1/A2: meus agendamentos, cancelar com confirmação
    first = [i for i in ids(r) if i.startswith("bk:")][0]
    r = talk(P, opcao=first)
    bid1 = first[3:]
    check("horário escolhido: Remarcar / Cancelar horário / Voltar",
          r["estado"] == "bookingAction" and ids(r) == [f"rem:{bid1}", f"cask:{bid1}", "nav:my"] and "Civic" in last(r)["texto"], ids(r))
    r = talk(P, opcao=f"cask:{bid1}")
    check("cancelar pede confirmação", ids(r) == [f"canc:{bid1}", "nav:my"], ids(r))
    r = talk(P, "sim")
    s, b = call("GET", A + f"/agendamentos/{bid1}", token=tok)
    check("'sim' confirma o cancelamento", "liberado" in texts(r) and b["status"] == "canceled", f"{texts(r)[:60]} {b.get('status')}")
    r = talk(P, "cancelar")
    check("'cancelar' sem fluxo em andamento lista os horários", "Não há agendamento em andamento" in texts(r) and r["estado"] == "myBookings", texts(r)[:100])
    r = talk(P, opcao="nav:my")
    r = talk(P, opcao=[i for i in ids(r) if i.startswith("bk:")][0])
    r = talk(P, opcao="nav:my")
    check("Voltar mostra a lista de novo", r["estado"] == "myBookings", r["estado"])

    # agendamento do balcão sem telefone, só ligado ao cliente: também é dele
    s, counter = call("POST", A + "/agendamentos", {"date": DAY, "start": 600, "boxId": None, "serviceId": "s1", "vehicleType": "Sedan",
                                                    "vehicle": "Civic", "plate": "CAR1A11", "clientName": "Carla Cadastro", "phone": "",
                                                    "notes": "", "origin": "counter", "reminderOptIn": False, "clientId": cid,
                                                    "vehicleId": civic["id"]}, token=tok)
    check("agendamento do balcão ligado ao cliente, sem telefone", s == 201, f"{s} {counter}")
    created_bookings.append(counter["id"])
    r = talk(P, "meus agendamentos")
    check("'meus agendamentos' inclui o do balcão", f"bk:{counter['id']}" in ids(r), ids(r))
    r = talk(P, opcao=f"bk:{counter['id']}")
    check("dono pelo cadastro: mostra as ações", ids(r)[:1] == [f"rem:{counter['id']}"], texts(r)[:80])
    r = talk(P, opcao=f"rem:{counter['id']}")
    check("remarcar pelo menu: escolhe o dia", r["estado"] == "choosingDay", r["estado"])
    r = talk(P, opcao=f"day:{DAY}")
    new_slot = next((i for i in ids(r) if i.startswith("slot:") and i != "slot:600"), None)
    r = talk(P, opcao=new_slot)
    check("remarcar pelo menu: resumo do novo horário", r["estado"] == "confirming" and "Novo horário" in last(r)["texto"], last(r)["texto"][:120])
    r = talk(P, opcao="confirm:ok")
    s, old = call("GET", A + f"/agendamentos/{counter['id']}", token=tok)
    check("remarcado pelo menu", "Remarcado" in texts(r) and old["status"] == "canceled", f"{texts(r)[:40]} {old.get('status')}")

    # ---- A8: cliente bloqueado e faltas
    s, c = call("GET", A + f"/clientes/{cid}", token=tok)
    form = {"name": c["name"], "phone": c["phone"], "reminderOptIn": c["reminderOptIn"], "vip": c["vip"], "notes": c["notes"],
            "botBlocked": True, "vehicles": c["vehicles"]}
    s, c2 = call("PUT", A + f"/clientes/{cid}", form, token=tok)
    check("bloquear o cliente no WhatsApp", s == 200 and c2.get("botBlocked") is True, f"{s} {c2 and c2.get('botBlocked')}")
    talk(P, "oi")
    r = talk(P, opcao="svc:s1")
    check("cliente bloqueado: o bot manda falar com a equipe", "feitos com a equipe" in texts(r) and "nav:attendant" in ids(r)
          and r["estado"] == "idle", texts(r)[:100])
    s, c3 = call("PUT", A + f"/clientes/{cid}", {**form, "botBlocked": False}, token=tok)
    check("desbloquear", s == 200 and c3.get("botBlocked") is False, s)

    P = PHONES["faltas"]
    for start in (540, 660):
        s, fb = call("POST", A + "/agendamentos", {"date": DAY, "start": start, "boxId": None, "serviceId": "s1", "vehicleType": "Hatch",
                                                   "vehicle": "Gol", "plate": "", "clientName": "Fábio Faltas", "phone": P, "notes": "",
                                                   "origin": "counter", "reminderOptIn": False}, token=tok)
        call("PATCH", A + f"/agendamentos/{fb['id']}/status", {"status": "no_show"}, token=tok)
    talk(P, "oi")
    r = talk(P, opcao="svc:s1")
    check("2 faltas recentes: agenda só com a equipe", "houve 2 faltas recentes" in texts(r) and ids(r) == ["nav:attendant"], texts(r)[:100])

    # ---- A5: caixa de entrada com atendimento humano
    P = PHONES["inbox"]
    talk(P, "oi", nome="Paula Inbox")
    r = talk(P, "atendente", nome="Paula Inbox")
    check("cliente pede atendente", r["estado"] == "withAttendant", r["estado"])
    s, inbox = call("GET", A + "/bot/conversas", token=tok)
    row = next((x for x in inbox or [] if x["telefone"] == P), None)
    check("caixa de entrada: conversa pedindo atendente, não lida, janela aberta",
          s == 200 and row and row["precisaAtendente"] and row["estado"] == "withAttendant" and row["naoLidas"] >= 2
          and row["janelaAberta"] and row["nome"] == "Paula Inbox" and row["ultima"]["direcao"] == "out", row)
    s, _ = call("POST", A + f"/bot/conversas/{P}/lida", token=tok)
    s, inbox = call("GET", A + "/bot/conversas", token=tok)
    row = next((x for x in inbox or [] if x["telefone"] == P), {})
    check("marcar como lida zera as não lidas", row.get("naoLidas") == 0, row.get("naoLidas"))
    s, conv = call("GET", A + f"/bot/conversas/{P}", token=tok)
    lst = next((m for m in conv["mensagens"] if m["tipo"] == "list"), None)
    check("conversa mostra as opções que o bot ofereceu", lst and lst["botao"] == "Ver serviços" and lst["opcoes"]
          and conv["janelaAberta"] and conv["janelaFechaEm"], lst)
    s, reply = call("POST", A + f"/bot/conversas/{P}/responder", {"texto": "Oi Paula, aqui é a equipe! Como posso ajudar?"}, token=tok)
    check("equipe responde pelo app", s == 200 and reply["direcao"] == "agent" and reply["autor"], f"{s} {reply}")
    r = talk(P, "obrigada!", nome="Paula Inbox")
    check("com a equipe, o bot não responde", not r["mensagens"] and r["estado"] == "withAttendant", r)
    s, conv = call("GET", A + f"/bot/conversas/{P}", token=tok)
    tail = [m["direcao"] for m in conv["mensagens"][-2:]]
    check("conversa: resposta da equipe e depois o cliente", tail == ["agent", "in"], tail)
    s, inbox = call("GET", A + "/bot/conversas", token=tok)
    row = next((x for x in inbox or [] if x["telefone"] == P), {})
    check("nova mensagem do cliente conta como não lida", row.get("naoLidas") == 1, row.get("naoLidas"))
    s, p = call("POST", A + f"/bot/conversas/{P}/devolver", token=tok)
    check("devolver ao bot", s == 200 and p["estado"] == "idle", f"{s} {p}")
    r = talk(P, "oi", nome="Paula Inbox")
    check("depois de devolver, o bot volta a responder", r["estado"] == "choosingService", r["estado"])
    s, p = call("POST", A + f"/bot/conversas/{P}/assumir", token=tok)
    r = talk(P, "oi", nome="Paula Inbox")
    check("equipe assume: bot calado", s == 200 and p["estado"] == "withAttendant" and not r["mensagens"], f"{s} {p} {r['mensagens']}")
    r = talk(P, "menu", nome="Paula Inbox")
    check("cliente manda 'menu' e volta ao bot", r["estado"] == "choosingService", r["estado"])
    s, p = call("POST", A + f"/bot/conversas/{P}/responder", {"texto": "   "}, token=tok)
    check("resposta vazia = 400", s == 400, f"{s} {p}")
    # Número que nunca escreveu (o reset do começo do teste abriria a janela dos números fixos).
    never = "55119" + str(int(time.time() * 1000))[-8:]
    s, p = call("POST", A + f"/bot/conversas/{never}/responder", {"texto": "Olá"}, token=tok)
    check("sem mensagem do cliente nas últimas 24 h = 409", s == 409 and "24" in (p or {}).get("mensagem", ""), f"{s} {p}")

    # ---- limites da Cloud API em tudo que saiu
    problems = []
    for m in sent:
        if m["tipo"] == "list":
            if len(m["opcoes"]) > 10 or len(m["texto"]) > 1024 or len(m["botao"] or "") > 20:
                problems.append(("list", len(m["opcoes"]), m["texto"][:30]))
            problems += [("row", o["titulo"]) for o in m["opcoes"] if len(o["titulo"]) > 24 or len(o["descricao"] or "") > 72]
        if m["tipo"] == "buttons":
            if len(m["opcoes"]) > 3 or len(m["texto"]) > 1024:
                problems.append(("buttons", m["texto"][:30]))
            problems += [("button", o["titulo"]) for o in m["opcoes"] if len(o["titulo"]) > 20]
        if m["tipo"] == "text" and len(m["texto"]) > 4096:
            problems.append(("text", len(m["texto"])))
    check(f"{len(sent)} mensagens dentro dos limites da Cloud API", not problems, problems[:5])

    # ---- modelo do lembrete para a Meta
    s, tpl = call("GET", A + "/bot/modelo-lembrete", token=tok)
    check("modelo do lembrete no formato da Meta", s == 200 and "{{1}}" in tpl["texto"] and tpl["botoes"] == ["Confirmar", "Remarcar", "Cancelar"]
          and tpl["categoria"] == "UTILITY" and "{" not in tpl["previa"], tpl)

    # ---- webhook
    sec = user_secrets()
    app_secret = os.environ.get("WHATSAPP_APP_SECRET") or sec.get("WhatsApp:AppSecret", "")
    verify = os.environ.get("WHATSAPP_VERIFY_TOKEN") or sec.get("WhatsApp:VerifyToken", "")
    number_id = os.environ.get("WHATSAPP_PHONE_NUMBER_ID") or sec.get("WhatsApp:Numbers:0:PhoneNumberId", "")
    if not (app_secret and verify and number_id):
        check("segredos do webhook configurados (AppSecret, VerifyToken, Numbers:0)", False, "configure os user-secrets")
    else:
        W = "/api/whatsapp/webhook"
        q = urllib.parse.urlencode({"hub.mode": "subscribe", "hub.verify_token": verify, "hub.challenge": "1158201444"})
        s, p = raw("GET", f"{W}?{q}")
        check("verificação devolve o challenge", s == 200 and str(p) == "1158201444", f"{s} {p}")
        q = urllib.parse.urlencode({"hub.mode": "subscribe", "hub.verify_token": "errado", "hub.challenge": "1"})
        s, _ = raw("GET", f"{W}?{q}")
        check("verificação com token errado = 403", s == 403, s)

        def envelope(value, pnid=None):
            return {"object": "whatsapp_business_account", "entry": [{"id": "WABA", "changes": [{"field": "messages", "value": {
                "messaging_product": "whatsapp", "metadata": {"display_phone_number": "551400000000", "phone_number_id": pnid or number_id},
                **value}}]}]}

        def message(phone, wam, **kind):
            return envelope({"contacts": [{"profile": {"name": "Paulo Webhook"}, "wa_id": phone}],
                             "messages": [{"from": phone, "id": wam, "timestamp": str(int(time.time())), **kind}]})

        def post(payload, signed=True, secret=None):
            body = json.dumps(payload).encode()
            headers = {"Content-Type": "application/json"}
            if signed:
                sig = hmac.new((secret or app_secret).encode(), body, hashlib.sha256).hexdigest()
                headers["X-Hub-Signature-256"] = "sha256=" + sig
            return raw("POST", W, body, headers)[0]

        def wait_conversation(phone, cond, seconds=30):
            end = time.time() + seconds
            conv = None
            while time.time() < end:
                s, conv = call("GET", A + f"/bot/conversas/{phone}", token=tok)
                if s == 200 and cond(conv):
                    return conv
                time.sleep(0.5)
            return conv

        def after(conv, wam_id):
            """Mensagens a partir da entrada com esse id (a conversa traz também o reset do início)."""
            msgs = conv["mensagens"] if conv else []
            i = next((k for k, m in enumerate(msgs) if m["wamId"] == wam_id), None)
            return [] if i is None else msgs[i:]

        P = PHONES["webhook"]
        wid = f"wamid.e2e.{uuid.uuid4().hex}"
        payload = message(P, wid, type="text", text={"body": "oi"})
        check("webhook sem assinatura = 401", post(payload, signed=False) == 401)
        check("webhook com assinatura errada = 401", post(payload, secret="outra-chave") == 401)
        check("webhook assinado = 200", post(payload) == 200)
        check("reenvio do mesmo id = 200", post(payload) == 200)
        conv = wait_conversation(P, lambda c: any(m["direcao"] == "out" for m in after(c, wid)) and c["estado"] == "choosingService")
        tail = after(conv, wid)
        check("webhook processado uma vez só", len([m for m in tail if m["direcao"] == "in"]) == 1
              and conv["estado"] == "choosingService", tail)
        time.sleep(2)
        tail = after(wait_conversation(P, lambda c: True), wid)
        check("o reenvio não gerou outra resposta", len([m for m in tail if m["direcao"] == "out"]) == 1, tail)
        check("webhook de lista: escolha volta como opção", post(message(P, f"wamid.e2e.{uuid.uuid4().hex}", type="interactive",
              interactive={"type": "list_reply", "list_reply": {"id": "svc:s1", "title": "x"}})) == 200)
        conv = wait_conversation(P, lambda c: c["estado"] == "choosingVehicleType")
        check("lista pelo webhook anda o fluxo", conv and conv["estado"] == "choosingVehicleType", conv and conv["estado"])
        bot(P, "cancelar")

        P = PHONES["desconhecido"]
        uid = f"wamid.e2e.{uuid.uuid4().hex}"
        unknown = message(P, uid, type="text", text={"body": "oi"})
        unknown["entry"][0]["changes"][0]["value"]["metadata"]["phone_number_id"] = "000000000000000"
        check("número sem filial = 200", post(unknown) == 200)
        time.sleep(3)
        s, conv = call("GET", A + f"/bot/conversas/{P}", token=tok)
        check("número sem filial é ignorado", s == 200 and not after(conv, uid), conv)
        bot(P, "cancelar")

        # lembrete enviado pelo template e status de entrega
        P = PHONES["entrega"]
        s, rb = call("POST", A + "/agendamentos", {**base, "phone": P, "start": 540, "reminderOptIn": True, "clientName": "Elisa Entrega"}, token=tok)
        check("agendamento com lembrete", s == 201, f"{s} {rb}")
        s, rems = call("GET", A + f"/lembretes?de={DAY}&ate={DAY}", token=tok)
        rem = next((x for x in rems or [] if x["bookingId"] == rb.get("id")), None)
        s, disp = call("POST", A + "/lembretes/processar", {"ate": DISPATCH_UNTIL, "ids": [rem["id"]] if rem else []}, token=tok)
        sent_rem = (disp or [{}])[0].get("reminder", {}) if s == 200 else {}
        wam = sent_rem.get("wamId")
        check("lembrete enviado pelo template", s == 200 and sent_rem.get("status") == "sent" and wam, f"{s} {disp}")
        conv = wait_conversation(P, lambda c: any(m["wamId"] == wam for m in c["mensagens"]), 5)
        tmsg = next((m for m in conv["mensagens"] if m["wamId"] == wam), None) if conv else None
        check("template registrado na conversa com a prévia", tmsg and tmsg["wamId"] == wam and "Elisa" in tmsg["texto"], tmsg)

        def status(st, err=None):
            item = {"id": wam, "status": st, "timestamp": str(int(time.time())), "recipient_id": P}
            if err:
                item["errors"] = [{"code": 131026, "title": "Message undeliverable"}]
            return envelope({"statuses": [item]})

        def reminder_status(expected, seconds=20):
            end = time.time() + seconds
            cur = None
            while time.time() < end:
                s, rems = call("GET", A + f"/lembretes?de={DAY}&ate={DAY}", token=tok)
                cur = next((x for x in rems or [] if x["id"] == rem["id"]), {})
                if cur.get("status") == expected:
                    return cur
                time.sleep(0.5)
            return cur

        check("status sent é aceito", post(status("sent")) == 200)
        check("status delivered", post(status("delivered")) == 200 and reminder_status("delivered").get("status") == "delivered")
        check("status read", post(status("read")) == 200 and reminder_status("read").get("status") == "read")
        post(status("delivered"))
        time.sleep(1.5)
        check("status não volta (read → delivered ignorado)", reminder_status("read", 2).get("status") == "read")

        bid_ = f"wamid.e2e.{uuid.uuid4().hex}"
        r_payload = message(P, bid_, type="button", button={"payload": f"conf:{rb['id']}", "text": "Confirmar"})
        check("botão do template pelo webhook = 200", post(r_payload) == 200)
        conv = wait_conversation(P, lambda c: any("Confirmado" in m["texto"] for m in after(c, bid_)))
        check("botão do template confirma o horário", any("Confirmado" in m["texto"] for m in after(conv, bid_)), after(conv, bid_))
        end = time.time() + 10
        cur = {}
        while time.time() < end:
            s, rems = call("GET", A + f"/lembretes?de={DAY}&ate={DAY}", token=tok)
            cur = next((x for x in rems or [] if x["id"] == rem["id"]), {})
            if cur.get("answer"):
                break
            time.sleep(0.5)
        check("resposta registrada no lembrete", cur.get("answer") == "confirmed", cur)
finally:
    cleanup_test_data()
    s, _ = call("PUT", A + "/config", original_cfg, token=tok)
    check("ajustes originais devolvidos", s == 200, s)

ok = sum(1 for _, x in results if x)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
