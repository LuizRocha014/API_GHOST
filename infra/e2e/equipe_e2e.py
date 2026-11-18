"""Teste ponta a ponta da equipe e da segurança da agenda (banco real).

Uso: python infra/e2e/equipe_e2e.py [http://127.0.0.1:5000]

Cobre: perfil no login, cadastro da equipe com senha provisória (regra de senha, login repetido), troca obrigatória da
senha antes de usar a agenda, o que o atendente não pode (ajustes, excluir, equipe, rotas genéricas), filiais por pessoa,
bloqueio depois de 5 senhas erradas e desbloqueio pelo dono, desativar (a sessão cai na hora), proteção do próprio acesso
de dono e o cadastro de filiais (criar, renomear, desativar).

Reaproveita os logins de teste (e2e.atendente, e2e.dono) a cada rodada e deixa os dois desativados no fim.
"""
import json
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
STAFF = "e2e.atendente"
OWNER2 = "e2e.dono"
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


def msg(p):
    return (p or {}).get("mensagem", "") if isinstance(p, dict) else str(p)


def login(user, password):
    return call("POST", "/api/auth/login", {"username": user, "password": password})


s, p = login("admin", "Admin@123")
if not check("login do dono", s == 200 and p.get("role") == "owner" and p.get("mustChangePassword") is False, f"{s} {p}"):
    sys.exit(1)
tok = p["accessToken"]
s, branches = call("GET", "/api/agenda/filiais", token=tok)
B1, B2 = branches[0]["id"], branches[1]["id"]
A1, A2 = f"/api/agenda/{B1}", f"/api/agenda/{B2}"
print("     filiais:", branches[0]["name"], "/", branches[1]["name"])

try:
    s, team = call("GET", A1 + "/equipe", token=tok)
    admin = next((m for m in team or [] if m["username"] == "admin"), None)
    check("equipe lista o dono", s == 200 and admin and admin["role"] == "owner" and admin["active"], f"{s} {team}")
    s, sug = call("GET", A1 + "/equipe/senha-provisoria", token=tok)
    temp = (sug or {}).get("senha", "")
    check("senha provisória sugerida", s == 200 and len(temp) == 10 and any(c.isdigit() for c in temp), sug)

    # ---- cadastro (ou reaproveita o atendente de teste)
    existing = {m["username"]: m for m in team or []}
    body = {"name": "Atendente Teste", "username": STAFF, "role": "staff", "branchIds": [], "temporaryPassword": "123"}
    if STAFF in existing:
        sid = existing[STAFF]["id"]
        s, p = call("PUT", A1 + f"/equipe/{sid}", {**body, "active": True}, token=tok)
        check("senha fraca na redefinição = 400", s == 400 and "8 caracteres" in msg(p), f"{s} {p}")
        s, p = call("PUT", A1 + f"/equipe/{sid}", {**body, "active": True, "temporaryPassword": temp}, token=tok)
        check("atendente de teste reativado com senha provisória", s == 200 and p["mustChangePassword"] and p["active"], f"{s} {p}")
    else:
        s, p = call("POST", A1 + "/equipe", body, token=tok)
        check("senha fraca no cadastro = 400", s == 400 and "8 caracteres" in msg(p), f"{s} {p}")
        s, p = call("POST", A1 + "/equipe", {**body, "temporaryPassword": temp}, token=tok)
        check("atendente cadastrado com senha provisória", s == 201 and p["role"] == "staff" and p["mustChangePassword"], f"{s} {p}")
        sid = p["id"]
    s, p = call("POST", A1 + "/equipe", {**body, "temporaryPassword": temp}, token=tok)
    check("login repetido = 409", s == 409, f"{s} {p}")

    # ---- primeiro acesso: só troca a senha
    s, p = login(STAFF, temp)
    check("atendente entra com a provisória e precisa trocar", s == 200 and p["role"] == "staff" and p["mustChangePassword"], f"{s} {p}")
    stok = p["accessToken"]
    s, p = call("GET", "/api/agenda/filiais", token=stok)
    check("com senha provisória a agenda responde 403", s == 403 and "provisória" in msg(p), f"{s} {p}")
    s, p = call("GET", A1 + "/config", token=stok)
    check("config também 403 antes de trocar", s == 403, s)
    s, p = call("POST", "/api/auth/senha", {"currentPassword": "errada", "newPassword": "NovaSenha2026"}, token=stok)
    check("trocar com a senha atual errada = 400", s == 400 and "atual" in msg(p), f"{s} {p}")
    s, p = call("POST", "/api/auth/senha", {"currentPassword": temp, "newPassword": "12345678"}, token=stok)
    check("senha nova fraca = 400", s == 400, f"{s} {p}")
    s, p = call("POST", "/api/auth/senha", {"currentPassword": temp, "newPassword": "Lavagem2026x"}, token=stok)
    check("senha trocada: token novo sem a troca pendente", s == 200 and p["mustChangePassword"] is False, f"{s} {p}")
    stok = p["accessToken"]

    # ---- o que o atendente pode e não pode
    s, cfg = call("GET", A1 + "/config", token=stok)
    check("atendente lê a configuração", s == 200 and cfg.get("services"), s)
    s, p = call("PUT", A1 + "/config", cfg, token=stok)
    check("atendente não grava ajustes (403)", s == 403 and "dono" in msg(p), f"{s} {p}")
    s, p = call("GET", A1 + "/agendamentos?de=2026-01-01&ate=2026-01-02", token=stok)
    check("atendente lista agendamentos", s == 200, s)
    s, p = call("DELETE", A1 + "/agendamentos/00000000-0000-0000-0000-000000000001", token=stok)
    check("atendente não exclui agendamento (403)", s == 403, s)
    s, p = call("DELETE", A1 + "/agendamentos?antesDe=2000-01-01", token=stok)
    check("atendente não limpa histórico (403)", s == 403, s)
    s, p = call("GET", A1 + "/equipe", token=stok)
    check("atendente não vê a equipe (403)", s == 403, s)
    s, p = call("GET", "/api/users", token=stok)
    check("rotas genéricas exigem dono (403)", s == 403, s)
    s, p = call("GET", "/api/users", token=tok)
    check("dono usa as rotas genéricas", s == 200, s)

    # ---- filiais por pessoa
    s, p = call("PUT", A1 + f"/equipe/{sid}", {"name": "Atendente Teste", "role": "staff", "active": True, "branchIds": [B2]}, token=tok)
    check("dono limita o atendente à segunda filial", s == 200 and p["branchIds"] == [B2], f"{s} {p}")
    s, lst = call("GET", "/api/agenda/filiais", token=stok)
    check("atendente só vê a filial dele", s == 200 and [b["id"] for b in lst] == [B2], lst)
    s, p = call("GET", A1 + "/config", token=stok)
    check("filial fora da lista = 403", s == 403, s)
    s, p = call("GET", A2 + "/config", token=stok)
    check("filial dele = 200", s == 200, s)
    call("PUT", A1 + f"/equipe/{sid}", {"name": "Atendente Teste", "role": "staff", "active": True, "branchIds": []}, token=tok)

    # ---- bloqueio por tentativas
    codes = [login(STAFF, f"errada{i}")[0] for i in range(5)]
    check("5 senhas erradas: 401 x4 e bloqueio no 5º (429)", codes == [401, 401, 401, 401, 429], codes)
    s, p = login(STAFF, "Lavagem2026x")
    check("bloqueado até com a senha certa", s == 429 and "minuto" in msg(p), f"{s} {p}")
    s, team = call("GET", A1 + "/equipe", token=tok)
    me = next(m for m in team if m["id"] == sid)
    check("equipe mostra o bloqueio", me["locked"] is True, me)
    s, p = call("PUT", A1 + f"/equipe/{sid}", {"name": "Atendente Teste", "role": "staff", "active": True, "branchIds": [],
                                               "temporaryPassword": "Provisoria55"}, token=tok)
    check("dono redefine a senha e desbloqueia", s == 200 and not p["locked"] and p["mustChangePassword"], f"{s} {p}")
    s, p = login(STAFF, "Provisoria55")
    check("entra com a nova provisória", s == 200 and p["mustChangePassword"], f"{s} {p}")
    s, p = call("POST", "/api/auth/senha", {"currentPassword": "Provisoria55", "newPassword": "Lavagem2026y"}, token=p["accessToken"])
    stok = p["accessToken"]

    # ---- desativar derruba a sessão
    s, p = call("PUT", A1 + f"/equipe/{sid}", {"name": "Atendente Teste", "role": "staff", "active": False, "branchIds": []}, token=tok)
    check("dono desativa o atendente", s == 200 and p["active"] is False, f"{s} {p}")
    s, p = call("GET", A1 + "/config", token=stok)
    check("token do desativado = 401 na hora", s == 401, f"{s} {p}")
    s, p = login(STAFF, "Lavagem2026y")
    check("desativado não entra", s == 401, s)
    s, p = call("POST", "/api/auth/renovar", token=stok)
    check("desativado não renova", s == 401, s)

    # ---- dono não tira o próprio acesso
    s, p = call("PUT", A1 + f"/equipe/{admin['id']}", {"name": admin["name"], "role": "staff", "active": True, "branchIds": []}, token=tok)
    check("dono não se rebaixa (400)", s == 400 and "próprio" in msg(p), f"{s} {p}")
    s, p = call("PUT", A1 + f"/equipe/{admin['id']}", {"name": admin["name"], "role": "owner", "active": False, "branchIds": []}, token=tok)
    check("dono não se desativa (400)", s == 400, f"{s} {p}")

    # ---- segundo dono
    body2 = {"name": "Dono Teste", "username": OWNER2, "role": "owner", "branchIds": [], "temporaryPassword": "SegundoDono9", "active": True}
    s, team = call("GET", A1 + "/equipe", token=tok)
    o2 = next((m for m in team if m["username"] == OWNER2), None)
    s, p = (call("PUT", A1 + f"/equipe/{o2['id']}", body2, token=tok) if o2 else call("POST", A1 + "/equipe", body2, token=tok))
    check("segundo dono cadastrado", s in (200, 201) and p["role"] == "owner", f"{s} {p}")
    oid = p["id"]
    s, p = login(OWNER2, "SegundoDono9")
    s, p = call("POST", "/api/auth/senha", {"currentPassword": "SegundoDono9", "newPassword": "DonoNovo2026"}, token=p["accessToken"])
    otok = p["accessToken"]
    s, p = call("GET", A1 + "/equipe", token=otok)
    check("segundo dono vê a equipe", s == 200, s)
    s, p = call("PUT", A1 + f"/equipe/{oid}", {**body2, "temporaryPassword": None, "active": False}, token=tok)
    check("um dono desativa o outro", s == 200 and p["active"] is False, f"{s} {p}")

    # ---- filiais da empresa
    s, lst = call("GET", A1 + "/empresa/filiais", token=tok)
    check("filiais da empresa", s == 200 and {b["id"] for b in lst} >= {B1, B2}, lst)
    s, nb = call("POST", A1 + "/empresa/filiais", {"name": "Filial Teste E2E"}, token=tok)
    check("cria filial", s == 201 and nb["name"] == "Filial Teste E2E", f"{s} {nb}")
    s, p = call("PUT", A1 + f"/empresa/filiais/{nb['id']}", {"name": "Filial Teste Renomeada"}, token=tok)
    check("renomeia filial", s == 200 and p["name"] == "Filial Teste Renomeada", f"{s} {p}")
    s, lst = call("GET", "/api/agenda/filiais", token=tok)
    check("dono sem vínculo vê a filial nova", any(b["id"] == nb["id"] for b in lst), lst)
    s, p = call("DELETE", A1 + f"/empresa/filiais/{B1}", token=tok)
    check("não desativa a filial aberta (400)", s == 400, f"{s} {p}")
    s, p = call("DELETE", A1 + f"/empresa/filiais/{nb['id']}", token=tok)
    check("desativa a filial de teste", s == 204, s)
    s, lst = call("GET", "/api/agenda/filiais", token=tok)
    check("filial desativada some da lista", not any(b["id"] == nb["id"] for b in lst), lst)
    s, p = call("PUT", A1 + "/empresa/filiais/" + nb["id"], {"name": "x"}, token=tok)
    check("filial desativada = 404", s == 404, s)
finally:
    # Deixa os usuários de teste desativados (o login fica reservado para a próxima rodada).
    s, team = call("GET", A1 + "/equipe", token=tok)
    for m in team or []:
        if m["username"] in (STAFF, OWNER2) and m["active"]:
            call("PUT", A1 + f"/equipe/{m['id']}", {"name": m["name"], "role": m["role"], "active": False, "branchIds": []}, token=tok)

ok = sum(1 for _, x in results if x)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
