"""Teste ponta a ponta do estacionamento: câmeras (imagem com senha, busca na rede), leituras de placa pela câmera com LPR
(JSON, XML da Hikvision, foto), mensalista, tickets (abrir, repetido, já aberto, fechar com valor da tabela, receber,
cancelar) e o pátio. Usa uma câmera falsa (imita a Hikvision) em 127.0.0.1:8000.

Uso: python infra/e2e/estacionamento_e2e.py [http://localhost:5000]
"""
import base64
import datetime as dt
import http.server
import json
import random
import string
import subprocess
import sys
import threading
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")
BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
JPEG = base64.b64decode("/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP////////////////////////////////////////////////////////////////"
                        "//////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=")
AUTH = "Basic " + base64.b64encode(b"admin:senha123").decode()
results = []


class FakeCamera(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if self.headers.get("Authorization") != AUTH:
            self.send_response(401)
            self.send_header("WWW-Authenticate", 'Basic realm="camera"')
            self.end_headers()
            return
        if self.path.startswith("/ISAPI/Streaming/channels/101/picture"):
            self.send_response(200)
            self.send_header("Content-Type", "image/jpeg")
            self.send_header("Content-Length", str(len(JPEG)))
            self.end_headers()
            self.wfile.write(JPEG)
        else:
            self.send_response(404)
            self.end_headers()

    def log_message(self, *a):
        pass


server = http.server.ThreadingHTTPServer(("127.0.0.1", 8000), FakeCamera)
threading.Thread(target=server.serve_forever, daemon=True).start()


def call(method, path, body=None, token=None, raw=None, ctype="application/json"):
    data = raw if raw is not None else (None if body is None else json.dumps(body).encode())
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", ctype)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            status, b, h = r.status, r.read(), r.headers.get("Content-Type", "")
    except urllib.error.HTTPError as e:
        status, b, h = e.code, e.read(), ""
    if h.startswith("image/"):
        return status, b
    try:
        return status, (json.loads(b) if b else None)
    except ValueError:
        return status, b.decode("utf-8", "replace")


def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("OK   " if cond else "FAIL ") + name + (f"  [{detail}]" if detail and not cond else ""))
    return cond


def plate():
    L = string.ascii_uppercase
    return "".join(random.choices(L, k=3)) + str(random.randint(0, 9)) + random.choice(L) + f"{random.randint(0, 99):02d}"


s, p = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"})
if not check("login", s == 200, s):
    sys.exit(1)
tok = p["accessToken"]
s, branches = call("GET", "/api/agenda/filiais", token=tok)
A = f"/api/agenda/{branches[0]['id']}/estacionamento"
cams, monthlies = [], []
M1, M2, R1, R2, R3, R4 = (plate() for _ in range(6))

try:
    s, cfg = call("GET", A + "/ajustes", token=tok)
    check("ajustes padrão", s == 200 and cfg["toleranceMin"] == 15 and float(cfg["firstHour"]) == 10, cfg)
    s, cfg = call("PUT", A + "/ajustes", {**cfg, "toleranceMin": 15, "firstHour": 10, "extraHour": 5, "dailyMax": 40,
                                          "dedupMinutes": 3, "minConfidence": 0.7, "enabled": True}, token=tok)
    check("tabela salva", s == 200, cfg)
    s, e = call("PUT", A + "/ajustes", {**cfg, "firstHour": -1}, token=tok)
    check("valor negativo = 400", s == 400, e)

    # ---- câmeras
    s, e = call("POST", A + "/cameras", {"name": "", "brand": "hikvision", "host": "127.0.0.1"}, token=tok)
    check("câmera sem nome = 400", s == 400, e)
    s, entry = call("POST", A + "/cameras", {"name": "Portão de entrada (teste)", "kind": "ip", "brand": "hikvision",
                                             "host": "127.0.0.1", "httpPort": 8000, "rtspPort": 554, "channel": 1,
                                             "username": "admin", "password": "senha123", "role": "entry"}, token=tok)
    cams.append(entry.get("id") if isinstance(entry, dict) else None)
    check("câmera Hikvision cadastrada (endereços da predefinição, senha escondida)", s == 200
          and entry["snapshotUrl"] == "http://127.0.0.1:8000/ISAPI/Streaming/channels/101/picture"
          and entry["rtspUrl"] == "rtsp://admin:***@127.0.0.1:554/Streaming/Channels/101" and entry["hasPassword"]
          and "password" not in entry and entry["ingestPath"].startswith("/api/estacionamento/leitura/"), entry)
    s, img = call("GET", A + f"/cameras/{entry['id']}/imagem", token=tok)
    check("imagem da câmera pela API (com usuário e senha)", s == 200 and isinstance(img, bytes) and img[:2] == b"\xff\xd8", s)
    s, bad = call("POST", A + "/cameras", {"name": "Senha errada (teste)", "brand": "hikvision", "host": "127.0.0.1",
                                           "httpPort": 8000, "username": "admin", "password": "errada"}, token=tok)
    cams.append(bad.get("id"))
    s, e = call("GET", A + f"/cameras/{bad['id']}/imagem", token=tok)
    check("senha errada: avisa o 401", s == 400 and "401" in e.get("mensagem", ""), e)
    s, lst = call("GET", A + "/cameras", token=tok)
    check("câmera com erro mostra o último erro", any(c["id"] == bad["id"] and c["lastError"] for c in lst), lst)
    s, dv = call("POST", A + "/cameras/descobrir?rede=127.0.0", token=tok)
    found = next((d for d in dv or [] if d["ip"] == "127.0.0.1"), None) if s == 200 else None
    check("busca na rede acha a câmera (porta 8000 = Hikvision)", found and 8000 in found["ports"] and found["brand"] == "hikvision",
          f"{s} {dv}")
    s, exitc = call("POST", A + "/cameras", {"name": "Portão de saída (teste)", "brand": "hikvision", "host": "127.0.0.1",
                                             "httpPort": 8000, "username": "admin", "password": "senha123", "role": "exit"}, token=tok)
    cams.append(exitc["id"])
    IN = entry["ingestPath"]
    OUT = exitc["ingestPath"]

    # ---- mensalista
    s, e = call("POST", A + "/mensalistas", {"plate": "12", "name": "X"}, token=tok)
    check("placa inválida = 400", s == 400, e)
    s, m = call("POST", A + "/mensalistas", {"plate": M1.lower(), "name": "Mensalista Teste", "vehicle": "Gol",
                                             "monthlyFee": 180}, token=tok)
    monthlies.append(m.get("id"))
    check("mensalista cadastrado (placa normalizada)", s == 200 and m["plate"] == M1 and m["valid"], m)
    s, e = call("POST", A + "/mensalistas", {"plate": M1, "name": "Outro"}, token=tok)
    check("mesma placa = 400", s == 400, e)
    yesterday = (dt.date.today() - dt.timedelta(days=1)).isoformat()
    s, m2 = call("POST", A + "/mensalistas", {"plate": M2, "name": "Vencido Teste", "validUntil": yesterday}, token=tok)
    monthlies.append(m2.get("id"))
    check("mensalista vencido", s == 200 and not m2["valid"], m2)

    # ---- leituras da câmera de entrada
    s, r = call("POST", IN, {"placa": M1, "confianca": 98})
    check("mensalista entra sem ticket", s == 200 and r["resultado"] == "monthly" and r["ticket"] is None, r)
    s, r = call("POST", IN, {"plate": R1, "confidence": 0.93})
    n1 = r.get("ticket")
    check("placa avulsa abre ticket", s == 200 and r["resultado"] == "ticket_opened" and n1, r)
    s, r = call("POST", IN, {"plate": R1})
    check("mesma placa logo em seguida = repetida", r["resultado"] == "duplicate", r)
    s, r = call("POST", A + "/leituras", {"plate": R1, "direction": "entry"}, token=tok)
    check("já tem ticket aberto", r["result"] == "already_open" and r["ticketNumber"] == n1, r)
    xml = f"<?xml version=\"1.0\"?><EventNotificationAlert><ANPR><licensePlate>{R2}</licensePlate><confidenceLevel>95</confidenceLevel></ANPR></EventNotificationAlert>"
    s, r = call("POST", IN, raw=xml.encode(), ctype="application/xml")
    check("XML da Hikvision (ANPR) abre ticket", s == 200 and r["resultado"] == "ticket_opened", r)
    s, r = call("POST", IN, {"placa": R3, "confianca": 40})
    check("pouca confiança só registra", r["resultado"] == "low_confidence" and r["ticket"] is None, r)
    s, r = call("POST", IN, {"placa": M2})
    check("mensalista vencido: abre ticket", r["resultado"] == "expired_monthly" and r["ticket"], r)
    boundary = "----e2e"
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"anpr.xml\"; filename=\"anpr.xml\"\r\nContent-Type: text/xml\r\n\r\n"
            f"<ANPR><licensePlate>{R4}</licensePlate></ANPR>\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"foto\"; "
            f"filename=\"placa.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n").encode() + JPEG + f"\r\n--{boundary}--\r\n".encode()
    s, r = call("POST", IN, raw=body, ctype=f"multipart/form-data; boundary={boundary}")
    check("multipart com XML e foto abre ticket", s == 200 and r["resultado"] == "ticket_opened", f"{s} {r}")
    s, r = call("POST", "/api/estacionamento/leitura/token-invalido", {"placa": R1})
    check("token inválido = 404", s == 404, r)
    s, r = call("POST", IN, {"texto": "sem placa"})
    check("sem placa = 400", s == 400, r)

    # ---- saída e valores
    s, open_ = call("GET", A + "/tickets?abertos=true", token=tok)
    t1 = next(t for t in open_ if t["plate"] == R1)
    subprocess.run(["sqlcmd", "-S", "localhost,1433", "-E", "-d", "AgendaGhost", "-b", "-I", "-Q",
                    f"UPDATE ParkingTickets SET EntryAt = DATEADD(minute, -130, EntryAt) WHERE Id = '{t1['id']}'"],
                   check=True, capture_output=True)
    s, r = call("POST", OUT, {"placa": R1})
    s, t = call("GET", A + "/tickets", token=tok)
    t1 = next(t for t in t if t["id"] == t1["id"])
    check("saída fecha o ticket: 2h10 = 1ª hora + 2 horas = R$ 20", r["resultado"] == "ticket_closed"
          and t1["status"] == "closed" and float(t1["amount"]) == 20 and t1["exitCamera"] == "Portão de saída (teste)", f"{r} {t1}")
    s, t1p = call("POST", A + f"/tickets/{t1['id']}/fechar", {"method": "pix"}, token=tok)
    check("receber depois de fechado", s == 200 and t1p["paymentMethod"] == "pix" and float(t1p["paidAmount"]) == 20, t1p)
    t2 = next(t for t in open_ if t["plate"] == R2)
    s, t2c = call("POST", A + f"/tickets/{t2['id']}/fechar", {"method": "cash", "amount": 7.5}, token=tok)
    check("fechar no balcão dentro da tolerância: R$ 0, recebe o valor informado", s == 200 and t2c["status"] == "closed"
          and float(t2c["amount"]) == 0 and float(t2c["paidAmount"]) == 7.5, t2c)
    s, r = call("POST", OUT, {"placa": plate()})
    check("saída sem ticket aberto", r["resultado"] == "no_ticket", r)
    s, r = call("POST", OUT, {"placa": M1})
    check("mensalista sai sem ticket", r["resultado"] == "monthly", r)
    s, mt = call("POST", A + "/tickets", {"plate": "abc1d23x"}, token=tok)
    check("ticket pelo balcão com placa inválida = 400", s == 400, mt)
    P5 = plate()
    s, mt = call("POST", A + "/tickets", {"plate": P5, "vehicle": "Fusca", "notes": "teste"}, token=tok)
    check("ticket pelo balcão", s == 200 and mt["plate"] == P5 and mt["status"] == "open" and mt["vehicle"] == "Fusca", mt)
    s, cc = call("POST", A + f"/tickets/{mt['id']}/cancelar", token=tok)
    check("cancelar ticket", s == 200 and cc["status"] == "canceled", cc)
    s, e = call("POST", A + f"/tickets/{t1['id']}/cancelar", token=tok)
    check("ticket pago não cancela = 400", s == 400, e)

    # ---- pátio e leituras
    s, y = call("GET", A + "/patio", token=tok)
    open_plates = [t["plate"] for t in y["open"]]
    check("pátio: abertos com valor até agora", s == 200 and M2 in open_plates and R4 in open_plates and R1 not in open_plates
          and all(t["amountNow"] is not None for t in y["open"]), y.get("open"))
    check("pátio: recebido hoje soma os pagos", float(y["receivedToday"]) >= 27.5, y.get("receivedToday"))
    s, rd = call("GET", A + "/leituras", token=tok)
    mine = [x for x in rd if x["plate"] in (M1, M2, R1, R2, R3, R4)]
    kinds = {x["result"] for x in mine}
    check("leituras registradas com o resultado", {"monthly", "ticket_opened", "duplicate", "already_open", "low_confidence",
                                                   "expired_monthly", "ticket_closed"} <= kinds, kinds)
    with_img = next((x for x in mine if x["plate"] == R4 and x["hasImage"]), None)
    s, img = call("GET", A + f"/leituras/{with_img['id']}/imagem", token=tok) if with_img else (0, None)
    check("foto da leitura guardada", s == 200 and isinstance(img, bytes) and img[:2] == b"\xff\xd8", s)
finally:
    s, open_ = call("GET", A + "/tickets?abertos=true", token=tok)
    for t in open_ if isinstance(open_, list) else []:
        if t["plate"] in (M1, M2, R1, R2, R3, R4):
            call("POST", A + f"/tickets/{t['id']}/cancelar", token=tok)
    for c in cams:
        if c:
            call("DELETE", A + f"/cameras/{c}", token=tok)
    for m in monthlies:
        if m:
            call("DELETE", A + f"/mensalistas/{m}", token=tok)
    server.shutdown()

ok = sum(1 for _, c in results if c)
print(f"\n{ok}/{len(results)} verificações OK")
sys.exit(0 if ok == len(results) else 1)
