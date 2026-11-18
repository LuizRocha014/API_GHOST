# Bot de agendamento pelo WhatsApp

O cliente manda mensagem para o WhatsApp da estética e agenda sozinho: escolhe o serviço, o tipo do veículo, o dia, o
horário, informa modelo e placa e confirma. Na véspera recebe o lembrete com os botões **Confirmar**, **Remarcar** e
**Cancelar**. Tudo passa pelas mesmas regras da agenda do app (disponibilidade, boxes, conflito, plano de fidelidade).

O bot é uma máquina de estados determinística, sem inteligência artificial: responde com listas e botões do próprio
WhatsApp e também aceita o que o cliente digita (o número da opção, o nome do serviço ou o horário, como `10:00` ou `10h`).

```
idle → choosingService → (choosingVehicle | choosingVehicleType) → choosingDay → choosingSlot → askingVehicle → confirming → idle
myBookings → bookingAction → (remarcar: choosingDay… | cancelar, com confirmação)
                                                    (qualquer passo) → withAttendant (a equipe responde)
```

O que o cliente consegue fazer sozinho:

- **Agendar**: serviço → veículo → dia → horário → resumo → confirmar. Cliente cadastrado escolhe um dos **carros
  dele** ("Civic · CAR1A11") e o bot não pergunta tipo, modelo nem placa; a reserva vai ligada ao cliente e ao carro.
- **Adicionais**: depois do veículo, o bot oferece os adicionais ativos (cera, aromatização…) um a um, até 3, com
  "Sem adicionais"/"Pronto". O resumo mostra os adicionais, o tempo e o preço somados (`bot.offerExtras` desliga).
- **Plano de fidelidade**: o resumo mostra "Incluso no seu plano *Ouro* (2º de 4 em novembro)" ou avisa que o plano
  já usou as vezes do mês e o horário sai pelo preço normal. Quem aplica o plano continua sendo a reserva.
- **Meus agendamentos**: lista os próximos horários (inclusive os marcados no balcão para o cliente) e, em cada um,
  **Remarcar** ou **Cancelar horário** (com confirmação), a qualquer momento, respeitando o prazo mínimo
  (`bot.cancelMinHours`). Também pelo botão do lembrete D-1.
- **Informações**: endereço, link do mapa, horário de funcionamento (das horas da agenda), preços (dos serviços),
  formas de pagamento e um recado. Sai com "endereço", "preços", "informações"… ou quando a primeira mensagem já é
  uma pergunta ("quanto custa a lavagem?"): o bot responde e mostra o menu. Configure em Ajustes → Negócio →
  Informações para o cliente (`info` da configuração).
- **Falar com a equipe**: o bot fica calado e a conversa aparece na caixa de entrada do app.
- **Lista de espera** (Ajustes › Lembretes › Lista de espera): nos dias lotados o bot oferece "🔔 Dia lotado? Te aviso se
  abrir vaga". Quando um horário abre (cancelamento, remarcação, bloqueio removido), o `WaitlistWorker` manda o modelo
  `vaga_disponivel` com o botão **Ver horários**, que leva direto aos horários livres daquele dia com o mesmo serviço.
  Avisa das 8h às 21h; quem marca horário no dia sai da lista ("agendou"). A equipe vê e mexe na lista pela Agenda.
- **Avaliação** (Ajustes › Lembretes): algumas horas depois de concluir, "Como foi o atendimento?" com **Ótimo**,
  **Bom** e **Ruim**. A nota fica no agendamento e nos relatórios; quem toca em Ruim cai na caixa de entrada para a
  equipe conversar.
- **Lembrete de novo** (Ajustes › Lembretes): quem não respondeu o lembrete da véspera recebe o mesmo modelo algumas
  horas antes do horário (dentro da janela de envio). Quem já respondeu não recebe.

Proteções do agendamento automático (Ajustes → Bot e mensagens; só valem para agendamento novo pelo WhatsApp):

| Ajuste | Padrão | Efeito |
| --- | --- | --- |
| `bot.maxUpcoming` | 3 | Com esse número de horários futuros, o bot pede para remarcar ou cancelar um antes de marcar outro. 0 = sem limite. |
| `bot.noShowLimit` / `bot.noShowDays` | 2 / 180 | Com 2 faltas nos últimos 180 dias, só a equipe marca. 0 = desligado. |
| Ficha do cliente → "Só agenda com a equipe" | – | O bot não marca para esse cliente (ele ainda vê, remarca e cancela o que tem). |

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
| `BotWorker` | `src/Api/Agenda` | Processa a fila (persistente, em `BotQueueItems`) e apaga a conversa fora da retenção a cada 6 h. |
| `ReminderWorker` | idem | A cada minuto envia lembretes, confirmações, carro pronto e avaliação vencidos. |
| `PlanBillingWorker` | idem | De hora em hora (9h–19h) avisa mensalidade vencendo e em atraso. |
| `WaitlistWorker` | idem | A cada minuto confere a lista de espera e avisa quem tem vaga. |

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
- **Botões da avaliação** (`rate5:`, `rate3:`, `rate1:` + id) e do **aviso de vaga** (`wl:` + id do pedido): também só
  valem para o número do cliente.
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
| `ReminderTemplate` | `lembrete_confirmacao` | Nome do template do lembrete aprovado na Meta (também o "lembrete de novo"). |
| `BookedTemplate`, `ReadyTemplate`, `ReviewTemplate` | `agendamento_confirmado`, `carro_pronto`, `avaliacao_atendimento` | Confirmação do balcão, carro pronto e avaliação. |
| `PlanDueTemplate`, `PlanOverdueTemplate` | `mensalidade_vencendo`, `mensalidade_atrasada` | Avisos de mensalidade. |
| `WaitlistTemplate` | `vaga_disponivel` | Aviso de vaga da lista de espera. |
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

- `GET api/agenda/{filial}/bot/modelo-lembrete`: o texto do lembrete no formato da Meta (`{{1}}`, `{{2}}`…), a ordem
  das variáveis e os botões, para cadastrar o template.

### Caixa de entrada (atendimento humano)

No app, aba **WhatsApp → Conversas** (só conectado à API). Rotas:

| Rota | O que faz |
| --- | --- |
| `GET bot/conversas` | Conversas dos últimos 30 dias: nome (do cadastro ou do perfil), passo do bot, `precisaAtendente`, `naoLidas`, `janelaAberta` e a última mensagem. |
| `GET bot/conversas/{telefone}` | A conversa inteira: cliente, bot (com as opções que ele mostrou) e equipe (com o autor). |
| `POST bot/conversas/{telefone}/responder` `{ "texto" }` | A equipe responde pelo WhatsApp. Só com a **janela de 24 h** aberta (o cliente escreveu nas últimas 24 h); fora dela, 409. A conversa passa para a equipe. |
| `POST bot/conversas/{telefone}/assumir` | A equipe assume: o bot para de responder (por até 12 h, ou até o cliente mandar "menu"). |
| `POST bot/conversas/{telefone}/devolver` | Devolve ao bot: a próxima mensagem do cliente recomeça o atendimento automático. |
| `POST bot/conversas/{telefone}/lida` | Zera as não lidas. |

A resposta da equipe é gravada em `BotMessages` com `Direction = agent` e o nome de quem está logado (`Author`).
O app atualiza a lista a cada 15 s e a conversa aberta a cada 4 s; o selo da aba WhatsApp conta quem pediu atendente.

### Teste ponta a ponta

`python infra/e2e/bot_e2e.py` testa tudo isso contra a API e o banco de verdade (113 verificações): o caminho feliz,
opção digitada, mensagem repetida, atendente, cancelar no meio, horário ocupado, as respostas do lembrete (dono, nono
dígito, prazo mínimo, remarcar), parar/voltar lembretes, áudio, os limites da Cloud API, o webhook (verificação,
assinatura, id repetido, número sem filial, status de entrega e botão do template) e a fase 2: informações, carros
cadastrados, plano no resumo (1º e 2º de 2, esgotado), limite de horários, meus agendamentos (cancelar com
confirmação, remarcar, agendamento do balcão sem telefone), cliente bloqueado, faltas e a caixa de entrada (não lidas,
responder, bot calado, devolver, assumir, janela fechada). Precisa de `AppSecret`, `VerifyToken` e `Numbers:0` nos
user-secrets (a filial do `Numbers:0` deve ser a primeira da lista). Devolve os ajustes originais e apaga o que cria.

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
6. **Modelos (templates):** abra `GET api/agenda/{filial}/bot/modelos` (ou o cartão de cada mensagem em Ajustes), e no
   **WhatsApp Manager → Modelos de mensagem** crie cada modelo com o **nome**, a categoria **Utilidade**, o idioma
   **Português (BR)**, o **corpo** exatamente como veio no campo `texto` (com `{{1}}`, `{{2}}`…; a Meta pede um exemplo
   para cada variável) e os botões de **resposta rápida** do campo `botoes`, nessa ordem:
   - `lembrete_confirmacao` (lembrete na véspera e o de novo): Confirmar, Remarcar, Cancelar;
   - `agendamento_confirmado` (confirmação do balcão, se ligar em Ajustes): Remarcar, Cancelar;
   - `carro_pronto` (se ligar em Ajustes): sem botões;
   - `avaliacao_atendimento` (se ligar em Ajustes): Ótimo, Bom, Ruim;
   - `mensalidade_vencendo` e `mensalidade_atrasada` (planos de fidelidade, se ligar em Ajustes): sem botões;
   - `vaga_disponivel` (lista de espera, se ligar em Ajustes): Ver horários.

   Espere a aprovação. Se mudar um texto nos Ajustes, o modelo precisa ser editado e aprovado de novo (a ordem das
   variáveis tem que bater). Os nomes podem ser trocados em `WhatsApp:ReminderTemplate`, `WhatsApp:BookedTemplate` e
   `WhatsApp:ReadyTemplate`.
7. Configure as chaves (seção acima), troque `Mode` para `Cloud`, reinicie a API e mande "oi" do seu celular para o
   número.
8. **Número próprio:** para atender clientes de verdade, adicione o número da estética na conta do WhatsApp Business
   (ele não pode estar em uso no app WhatsApp comum ao mesmo tempo), faça a **verificação da empresa** no Business Manager
   e defina o nome de exibição. Troque o `PhoneNumberId` na configuração.

Custos: as respostas do bot vão dentro da janela de 24 h aberta pela mensagem do cliente. O lembrete D-1 é template de
utilidade enviado fora da janela e é cobrado pela Meta por mensagem; confira a tabela de preços do WhatsApp Business
para o Brasil.

## Publicar a API

A Meta só chama URL https pública com certificado válido. O passo a passo (VPS com Docker e HTTPS automático, Azure ou
o computador da estética com Cloudflare Tunnel, mais o backup) está em [deploy.md](deploy.md).

## Limitações conhecidas (próximas fases)

- A fila entre o webhook e o bot é **persistente** (`BotQueueItems`, até 5 tentativas com espera crescente), mas um
  único worker processa as mensagens em ordem; para muitas estéticas no mesmo servidor, processar por telefone em
  paralelo.
- O app recebe as mudanças na hora por long-poll (`GET mudancas`), com alerta na tela; **notificação no celular com o
  app fechado** (Firebase Cloud Messaging) ainda não existe. Fotos e áudios do cliente aparecem só como
  "[image]"/"[audio]".
- Nos dados de exemplo do app (sem API) não há caixa de entrada: o simulador usa o bot local em Dart, que não tem as
  funções da fase 2.
- Falhas de envio por limite de taxa da Meta não são reenviadas automaticamente (o lembrete tenta de novo até 3 vezes).
