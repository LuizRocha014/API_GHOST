# Estacionamento rotativo e câmeras

Câmeras IP e canais de DVR/NVR ligados à agenda. Uma câmera marcada como **Entrada** lê a placa de quem chega:

- **Mensalista** em dia passa direto.
- **Mensalidade vencida**: abre um ticket e avisa.
- **Placa sem cadastro**: abre um **ticket rotativo**.

A câmera de **Saída** fecha o ticket com o valor da tabela. O app mostra o pátio, recebe o pagamento e cadastra
mensalistas e câmeras.

> **A API precisa estar na mesma rede das câmeras.** Ela busca as imagens e varre a rede. Hospedada na nuvem, ela não
> enxerga as câmeras da loja. Nesse caso rode a API num PC da loja; a [publicação](deploy.md) com Cloudflare Tunnel
> serve.

## Como a placa chega

Há três caminhos, que podem ser usados juntos.

1. **Câmera com LPR embutido (recomendado).** Hikvision ANPR, Intelbras VIP LPR e Dahua ITC leem a placa sozinhas e
   avisam um endereço HTTP.
   - O cartão da câmera no app mostra esse endereço:
     `http://SERVIDOR:5000/api/estacionamento/leitura/{token}`.
   - Ele é anônimo; o token secreto de cada câmera é a proteção.
   - Configure-o na câmera em *Evento → Alarme → HTTP Listening / Servidor de alarme* (Hikvision) ou
     *Evento → ITC → Upload HTTP* (Intelbras/Dahua).
   - Formatos aceitos:
     - JSON `{"plate":"ABC1D23","confidence":0.92,"direction":"entry"}`;
     - o XML de evento ANPR da Hikvision;
     - multipart com o XML e a foto.
2. **Leitura automática pela API.** É para câmeras comuns.
   - Ligue "Ler a placa sozinha" na câmera de entrada ou de saída.
   - O `ParkingWorker` pega uma imagem a cada `pollSeconds` e a compara com a anterior.
   - Quando muda mais que `motionPercent` da imagem, ele manda a foto ao
     [Plate Recognizer](https://platerecognizer.com), que tem um plano grátis de 2.500 leituras por mês.
   - Só roda com `Parking:PlateRecognizerToken` configurado.
   - O botão **Ler placa agora** do app faz a mesma leitura na hora, para testar o enquadramento.
3. **Digitada no app.** Em *Estacionamento → Pátio*: placa → Entrada ou Saída. Serve sem câmera ou quando a leitura
   falha.

Regras de cada leitura (`ParkingService.ProcessReadingAsync`):

| Resultado | Quando |
|---|---|
| `monthly` | mensalista ativo e na validade (entrada e saída liberadas, sem ticket) |
| `ticket_opened` | entrada de placa sem cadastro: novo ticket com número sequencial da filial |
| `expired_monthly` | mensalista vencido ou inativo: abre ticket e avisa |
| `already_open` | a placa já tem ticket aberto (não abre outro) |
| `ticket_closed` | saída: fecha o ticket aberto com o valor da tabela (o pagamento é recebido no app) |
| `no_ticket` | saída sem ticket aberto |
| `duplicate` | a mesma placa na mesma câmera dentro de `dedupMinutes` (o carro parado na frente da câmera) |
| `low_confidence` | leitura abaixo de `minConfidence` (fica registrada, não abre ticket) |
| `invalid_plate` | não é placa antiga (ABC1234) nem Mercosul (ABC1D23) |

Toda leitura fica no histórico com a foto. A foto é apagada depois de `Parking:ImageRetentionDays` dias; o texto
continua.

## Tabela de preço

Fica em `ajustes`, por filial; só o dono muda.

- **Tolerância:** os primeiros minutos são grátis (15 por padrão).
- **Por hora:** 1ª hora mais cada hora adicional. A fração de hora conta como hora inteira.
- **Teto diário:** a diária máxima vale para cada 24 h; 0 = sem teto.

O app mostra o "se sair agora" de cada ticket aberto e tem um simulador na aba Tarifas.

## Câmeras

- **Câmera IP**: IP, usuário, senha e marca.
- **Canal de DVR/NVR**: o IP do DVR mais o número do canal.

Pela marca a API monta os endereços:

| Marca | Imagem (snapshot) | Vídeo (RTSP) |
|---|---|---|
| Hikvision | `/ISAPI/Streaming/channels/{canal}01/picture` | `rtsp://IP:554/Streaming/Channels/{canal}01` |
| Intelbras / Dahua | `/cgi-bin/snapshot.cgi?channel={canal}` | `rtsp://IP:554/cam/realmonitor?channel={canal}&subtype=0` |
| Outra | o endereço informado | o endereço informado |

Como a imagem chega ao app:

- O app nunca fala direto com a câmera: pede `cameras/{id}/imagem` e a API busca com a senha, por Basic ou Digest.
- O "Ao vivo" pede uma imagem por segundo, o que dá para conferir o enquadramento.
- Para vídeo fluido, o RTSP pode ser copiado para o VLC.

**Buscar na rede** (`POST cameras/descobrir?rede=192.168.0`, só o dono) faz duas coisas ao mesmo tempo:

- manda o *probe* ONVIF (WS-Discovery), que traz nome e marca;
- varre a faixa /24 do servidor (ou a informada) nas portas 554, 80, 8080, 8000 (Hikvision), 37777 (Intelbras/Dahua) e
  34567 (DVRs genéricos).

Leva até ~30 s. No app, "Adicionar" já preenche IP, marca e tipo.

A senha da câmera fica gravada no banco para a API buscar a imagem. Ela nunca volta pela API (`hasPassword`). Use um
usuário só de visualização na câmera.

## Endpoints

Base `api/agenda/{filial}/estacionamento` (JWT; os marcados com ★ são só do dono).

| Método e rota | O que faz |
|---|---|
| `GET ajustes` / `PUT ajustes` ★ | tabela e regras da leitura |
| `GET patio` | tickets abertos com o valor até agora, mensalistas dentro, entradas, saídas, recebido e as últimas leituras |
| `GET cameras` · `POST` ★ · `PUT {id}` ★ · `DELETE {id}` ★ | câmeras |
| `GET cameras/{id}/imagem` | imagem agora (JPEG/PNG) |
| `POST cameras/{id}/ler` | imagem + leitura na nuvem + regra (teste) |
| `POST cameras/descobrir?rede=` ★ | busca na rede |
| `GET mensalistas` · `POST` · `PUT {id}` · `DELETE {id}` ★ | mensalistas |
| `GET tickets?de&ate&abertos` · `POST tickets` | tickets; abrir à mão |
| `POST tickets/{id}/fechar` `{method, amount}` | saída e/ou pagamento (sem `method` só fecha) |
| `POST tickets/{id}/cancelar` | cancela (não se já pago) |
| `POST leituras` `{plate, direction}` · `GET leituras?dias=` · `GET leituras/{id}/imagem` | placa digitada, histórico e foto |
| `POST /api/estacionamento/leitura/{token}` | anônimo: a câmera com LPR avisa a placa |

O app atualiza sozinho pelo feed de mudanças (tipo `parking`).

## Configuração

```json
"Parking": {
  "PlateRecognizerToken": "",
  "Region": "br",
  "ImageRetentionDays": 30
}
```

Guarde o token nos user-secrets (`dotnet user-secrets set "Parking:PlateRecognizerToken" "..." --project src/Api`) ou
na variável `Parking__PlateRecognizerToken`. Sem ele, câmeras com LPR embutido e a placa digitada continuam
funcionando.

## Testar na rede das câmeras

1. Rode a API no PC da rede das câmeras com o perfil `rede`; libere a porta 5000 no firewall.
2. No app (conectado a essa API): *Estacionamento → Câmeras → Buscar na rede*. Adicione a câmera, informe usuário e
   senha, e veja a imagem no cartão e no "Ao vivo".
3. Marque a câmera do portão como **Entrada**. Depois escolha uma das opções:
   - copie o endereço do cartão para o LPR da câmera;
   - ou ligue "Ler a placa sozinha" (com o token).
4. Cadastre um mensalista e passe com o carro dele, depois com um carro qualquer. O Pátio mostra a liberação e o ticket
   aberto.

`python infra/e2e/estacionamento_e2e.py` cobre tudo isso contra o banco, com uma câmera falsa em 127.0.0.1:8000
(37 verificações). Ela testa:

- imagem com senha certa e errada;
- busca na rede;
- leituras em JSON, XML e com foto;
- mensalista, vencido, repetida, já aberto, saída, sem ticket e placa inválida;
- tabela, receber, cancelar e pátio.
