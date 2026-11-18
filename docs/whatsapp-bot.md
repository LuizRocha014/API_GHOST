# Bot de agendamento pelo WhatsApp

O cliente manda mensagem para o WhatsApp da estética e agenda sozinho: escolhe o serviço, o tipo do veículo, o dia, o
horário, informa modelo e placa e confirma. Na véspera recebe o lembrete com os botões **Confirmar**, **Remarcar** e
**Cancelar**. Tudo passa pelas mesmas regras da agenda do app (disponibilidade, boxes, conflito, plano de fidelidade).

O bot é uma máquina de estados determinística, sem inteligência artificial: responde com listas e botões do próprio
WhatsApp e também aceita o que o cliente digita (o número da opção, o nome do serviço ou o horário, como `10:00` ou `10h`).

```
idle → choosingService → choosingVehicleType → choosingDay → choosingSlot → askingVehicle → confirming → idle
                                                    (qualquer passo) → withAttendant (a equipe responde)
```

## Como funciona por dentro

```
Meta ──POST /api/whatsapp/webhook──► WhatsAppWebhookController ──► BotQueue ──► BotWorker ──► BookingFlow
       (assinatura X-Hub-Signature-256)    valida, enfileira, 200           (um por vez)         │
                                                                                                 ▼
cliente ◄── Cloud API (graph.facebook.com) ◄── CloudMessagingChannel ◄────────────── IAgendaService + BotStore
```

| Peça | Onde | O que faz |
| --- | --- | --- |
| `BookingFlow` | `src/Application/Bot` | O fluxo da conversa (porta fiel do `booking_flow.dart` do app). |
| `BotStore` | `src/Infrastructure/Persistence` | Sessão por telefone (`BotSessions`), ids já processados (`BotProcessedMessages`) e a conversa (`BotMessages`). |
| `CloudMessagingChannel` | `src/Infrastructure/WhatsApp` | Envia pela WhatsApp Cloud API e devolve o `wamid`. |
| `LogMessagingChannel` | idem | Modo `Log`: não envia nada, só registra. |
| `WhatsAppReminderSender` | idem | Lembrete D-1 pelo template aprovado (substituiu o `LocalReminderSender`). |
| `WhatsAppWebhookController` | `src/Api/Controllers` | Verificação da Meta (GET) e recebimento (POST). |
| `BotWorker` | `src/Api/Agenda` | Processa a fila e apaga a conversa fora da retenção a cada 6 h. |

Regras que valem em qualquer passo:

- **Comandos:** `menu` (recomeça), `cancelar` (desiste do agendamento em andamento), `atendente` (passa para a equipe;
  o bot fica calado por até 12 h ou até o cliente mandar `menu`), `parar lembretes` e `quero lembretes`.
- **Mensagem repetida:** a Meta reenvia o webhook quando não recebe 200 a tempo. O id da mensagem é gravado antes de
  processar; o reenvio é ignorado e não agenda duas vezes.
- **Sessão parada** mais de `bot.sessionTimeoutMin` minutos (padrão 30) recomeça do início.
- **Entrada inválida:** repete a pergunta; depois de `bot.maxInvalidAttempts` erros (padrão 3), oferece atendente.
- **Horário ocupado** entre a escolha e a confirmação: avisa e manda a lista de novo.
- **Respostas do lembrete** (`conf:`, `rem:`, `canc:` + id): conferem se o número é o dono do agendamento (aceita o
  número com e sem o nono dígito) e respeitam `bot.cancelMinHours` (dentro do prazo, só com a equipe). Remarcar mantém o
  horário antigo até o novo ser reservado.
- **Áudio, foto, figurinha:** o bot pede para responder em texto ou tocar numa opção.

Os textos (saudação, agendado, confirmado, atendente, lembrete), os dias à frente, a antecedência mínima, perguntar ou
não o veículo e mostrar ou não o preço ficam nos Ajustes do app (`bot` e `texts` da configuração).

## Configuração

Seção `WhatsApp` do `appsettings` (os segredos ficam fora do repositório):

| Chave | Padrão | Para quê |
| --- | --- | --- |
| `Mode` | `Log` | `Log` não envia nada para a Meta; `Cloud` envia de verdade. |
| `GraphVersion` | `v24.0` | Versão da Graph API. |
| `AccessToken` | – | **Segredo.** Token permanente do usuário do sistema. |
| `AppSecret` | – | **Segredo.** Chave secreta do app (valida a assinatura do webhook). |
| `VerifyToken` | – | **Segredo.** Texto que você inventa e repete na tela do webhook da Meta. |
| `Numbers` | `[]` | Lista `{ PhoneNumberId, BranchId }`: qual número do WhatsApp atende cada filial. |
| `ReminderTemplate` | `lembrete_confirmacao` | Nome do template do lembrete aprovado na Meta. |
| `TemplateLanguage` | `pt_BR` | Idioma do template. |
| `RetentionDays` | `90` | Dias que a conversa fica guardada (LGPD). |

No desenvolvimento, com `dotnet user-secrets` (na raiz do repositório):

```bash
dotnet user-secrets set "WhatsApp:Mode" "Cloud" --project src/Api
dotnet user-secrets set "WhatsApp:AccessToken" "<token>" --project src/Api
dotnet user-secrets set "WhatsApp:AppSecret" "<chave secreta do app>" --project src/Api
dotnet user-secrets set "WhatsApp:VerifyToken" "<texto que você inventar>" --project src/Api
dotnet user-secrets set "WhatsApp:Numbers:0:PhoneNumberId" "<phone number id>" --project src/Api
dotnet user-secrets set "WhatsApp:Numbers:0:BranchId" "<id da filial>" --project src/Api
```

No servidor, as mesmas chaves como variáveis de ambiente: `WhatsApp__Mode`, `WhatsApp__AccessToken`,
`WhatsApp__Numbers__0__PhoneNumberId` e assim por diante.

A API escreve na subida o modo, quantos números estão configurados e se a assinatura do webhook está sendo conferida.
Sem `AppSecret`, o webhook só aceita mensagens em Development; em produção recusa tudo.

## Rodar o bot sem WhatsApp

Com a API no ar, o simulador passa pelo mesmo fluxo do webhook e devolve as respostas no corpo:

```bash
curl -X POST http://localhost:5000/api/agenda/<filial>/bot/mensagens \
  -H "Authorization: Bearer <jwt>" -H "Content-Type: application/json" \
  -d '{"telefone":"5511987654321","texto":"oi","nome":"Maria"}'
```

Para tocar numa opção, mande `opcaoId` (ex.: `svc:s1`, `slot:540`, `confirm:ok`, `conf:<id do agendamento>`).

- `GET api/agenda/{filial}/bot/conversas/{telefone}`: passo atual e as últimas mensagens da conversa.
- `GET api/agenda/{filial}/bot/modelo-lembrete`: o texto do lembrete no formato da Meta (`{{1}}`, `{{2}}`…), a ordem
  das variáveis e os botões, para cadastrar o template.

`python infra/e2e/bot_e2e.py` testa tudo isso contra a API e o banco de verdade: o caminho feliz, opção digitada,
mensagem repetida, atendente, cancelar no meio, horário ocupado, as respostas do lembrete (dono, nono dígito, prazo
mínimo, remarcar), parar/voltar lembretes, áudio, os limites da Cloud API, e o webhook (verificação, assinatura, id
repetido, número sem filial, status de entrega e botão do template). Precisa de `AppSecret`, `VerifyToken` e
`Numbers:0` nos user-secrets (a filial do `Numbers:0` deve ser a primeira da lista). Devolve os ajustes originais e apaga
o que cria.

## Ligar na Meta (passo a passo)

Isto é feito por você, com a conta do Facebook da estética. Leva de minutos (número de teste) a alguns dias
(verificação da empresa e aprovação do template).

1. Em [developers.facebook.com](https://developers.facebook.com), **Criar app** → tipo **Empresa** → adicione o produto
   **WhatsApp**. A Meta cria uma conta do WhatsApp Business e um **número de teste**.
2. Em **WhatsApp → Configuração da API** anote o **Identificador do número de telefone** (`PhoneNumberId`). O número de
   teste só manda mensagem para até 5 números cadastrados em **Para**: cadastre o seu celular.
3. **Token:** o da tela de configuração dura 24 h (serve para o primeiro teste). Para valer, em
   [business.facebook.com](https://business.facebook.com) → Configurações → **Usuários do sistema** → crie um usuário
   do sistema admin, atribua o app e a conta do WhatsApp e gere um token **sem expiração** com as permissões
   `whatsapp_business_messaging` e `whatsapp_business_management`. Esse é o `AccessToken`.
4. **Chave secreta do app:** Configurações do app → **Básico** → Chave secreta do app. Esse é o `AppSecret`.
5. **Webhook:** a Meta precisa de uma URL **https pública**. No desenvolvimento, use um túnel apontando para a API:

   ```bash
   cloudflared tunnel --url http://localhost:5000
   ```

   (ou `ngrok http 5000`). Em **WhatsApp → Configuração → Webhook**: URL de retorno
   `https://<endereço do túnel>/api/whatsapp/webhook`, token de verificação = o seu `VerifyToken`. Clique em
   **Verificar e salvar** e assine o campo **messages**. O endereço do túnel rápido muda a cada execução; para algo fixo,
   use um túnel nomeado ou publique a API (ver abaixo).
6. **Template do lembrete:** abra `GET api/agenda/{filial}/bot/modelo-lembrete`, e no **WhatsApp Manager → Modelos de
   mensagem** crie o modelo com o **nome**, a categoria **Utilidade**, o idioma **Português (BR)**, o **corpo** exatamente
   como veio no campo `texto` (com `{{1}}`, `{{2}}`…; a Meta pede um exemplo para cada variável) e três botões de
   **resposta rápida**: Confirmar, Remarcar e Cancelar, nessa ordem. Espere a aprovação. Se mudar o texto do lembrete
   nos Ajustes, o template precisa ser editado e aprovado de novo (a ordem das variáveis tem que bater).
7. Configure as chaves (seção acima), troque `Mode` para `Cloud`, reinicie a API e mande "oi" do seu celular para o
   número.
8. **Número próprio:** para atender clientes de verdade, adicione o número da estética na conta do WhatsApp Business
   (ele não pode estar em uso no app WhatsApp comum ao mesmo tempo), faça a **verificação da empresa** no Business Manager
   e defina o nome de exibição. Troque o `PhoneNumberId` na configuração.

Custos: as respostas do bot vão dentro da janela de 24 h aberta pela mensagem do cliente. O lembrete D-1 é template de
utilidade enviado fora da janela e é cobrado pela Meta por mensagem; confira a tabela de preços do WhatsApp Business
para o Brasil.

## Publicar a API

A Meta só chama URL https pública com certificado válido. Qualquer hospedagem de .NET serve (Azure App Service, uma VM
com IIS ou Nginx, um container). Lembre de configurar as variáveis de ambiente (`ConnectionStrings__Remote`,
`Jwt__Key`, `WhatsApp__*`) e deixar `ASPNETCORE_ENVIRONMENT=Production`.

## Limitações conhecidas (próximas fases)

- A fila entre o webhook e o bot é **em memória**: se a API cair depois de responder 200 e antes de processar, aquela
  mensagem se perde (a Meta não reenvia). Uma fila persistente resolve.
- Um único worker processa as mensagens em ordem; para muitas estéticas no mesmo servidor, processar por telefone em
  paralelo.
- A equipe ainda não vê nem responde a conversa pelo app (as mensagens ficam em `BotMessages` e na rota
  `bot/conversas`). Quando o cliente pede atendente, alguém precisa responder pelo WhatsApp Business.
- Falhas de envio por limite de taxa da Meta não são reenviadas automaticamente (o lembrete tenta de novo até 3 vezes).
