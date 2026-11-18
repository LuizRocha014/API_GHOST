"""Teste ponta a ponta do aviso de mudanças (GET agenda/{filial}/mudancas, long-poll) e do /health.

Uso: python infra/e2e/tempo_real_e2e.py [http://127.0.0.1:5000]

Cobre: versão inicial, espera sem mudança (volta vazio no tempo pedido), aviso na hora quando outro aparelho grava
(bloqueio), quando o bot responde (inbox) e quando a gravação falha (não avisa), versão antiga = tudo, e o /health.
Apaga o bloqueio que cria e a conversa de teste.
"""
import datetime as dt
import json
import sys
import threading
import time
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
PHONE = "5511990000501"
results = []


def call(method, path, body=None, token=None, timeout=90):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
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
day = (dt.date.today() + dt.timedelta(days=60)).isoformat()
block_id = None


def wait_changes(since, espera, out):
    t0 = time.time()
    out["status"], out["body"] = call("GET", A + f"/mudancas?desde={since}&espera={espera}", token=tok)
    out["seconds"] = time.time() - t0


try:
    s, v0 = call("GET", A + "/mudancas", token=tok)
    check("versão atual sem esperar", s == 200 and isinstance(v0.get("version"), int) and v0.get("kinds") == [], f"{s} {v0}")
    version = v0["version"]

    out = {}
    wait_changes(version, 2, out)
    check("sem mudança: espera o tempo pedido e volta vazio", out["status"] == 200 and out["body"]["kinds"] == []
          and 1.5 <= out["seconds"] <= 6, out)

    # outro aparelho grava um bloqueio enquanto este espera
    out = {}
    t = threading.Thread(target=wait_changes, args=(version, 20, out))
    t.start()
    time.sleep(1.0)
    s, blk = call("POST", A + "/bloqueios", {"boxId": None, "date": day, "start": 600, "end": 660, "reason": "Teste tempo real"}, token=tok)
    block_id = blk.get("id") if s == 200 else None
    t.join(25)
    check("bloqueio gravado avisa na hora (blocks)", out.get("status") == 200 and "blocks" in out["body"]["kinds"]
          and out["seconds"] < 5 and out["body"]["version"] > version, out)
    version = out["body"]["version"]

    # gravação recusada não avisa
    out = {}
    t = threading.Thread(target=wait_changes, args=(version, 3, out))
    t.start()
    time.sleep(0.5)
    s, _ = call("POST", A + "/bloqueios", {"boxId": None, "date": day, "start": 700, "end": 650, "reason": "x"}, token=tok)
    t.join(10)
    check("gravação com erro não avisa", s == 400 and out.get("body", {}).get("kinds") == [], f"{s} {out}")

    # o bot responde no simulador: avisa a caixa de entrada
    out = {}
    t = threading.Thread(target=wait_changes, args=(version, 20, out))
    t.start()
    time.sleep(0.5)
    call("POST", A + "/bot/mensagens", {"telefone": PHONE, "texto": "oi", "nome": "Tempo Real"}, token=tok)
    t.join(25)
    check("mensagem no bot avisa a caixa de entrada (inbox)", out.get("status") == 200 and "inbox" in out["body"]["kinds"]
          and out["seconds"] < 5, out)

    s, old = call("GET", A + "/mudancas?desde=1&espera=0", token=tok)
    check("versão desconhecida (de antes de reiniciar) devolve tudo", s == 200 and set(old["kinds"]) >= {"bookings", "config", "inbox"}, old)

    s, h = call("GET", "/health")
    check("/health responde sem login", s == 200 and h.get("banco") == "ok" and "fila" in h and "lembretes" in h, f"{s} {h}")
finally:
    if block_id:
        call("DELETE", A + f"/bloqueios/{block_id}", token=tok)
    call("POST", A + "/bot/mensagens", {"telefone": PHONE, "texto": "cancelar"}, token=tok)

ok = sum(1 for _, x in results if x)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
