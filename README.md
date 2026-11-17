# API_GHOST

API REST em ASP.NET Core para gestão de empresas, filiais, usuários, produtos, estoque, vendas e produção. A solução segue uma arquitetura em camadas (Domain, Application, Infrastructure, Api) com autenticação JWT e persistência em SQL Server via Entity Framework Core.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server acessível pela connection string configurada (LocalDB, instância local ou servidor remoto)

## Estrutura da solução

| Projeto | Função |
|--------|--------|
| `src/Api` | Host web, controllers, Swagger, autenticação JWT |
| `src/Application` | Casos de uso, serviços, DTOs e contratos (`I*Repository`, `I*Service`) |
| `src/Domain` | Entidades e enums de domínio |
| `src/Infrastructure` | EF Core (`AppDbContext`), repositórios, segurança (hash de senha, emissão de JWT) |

Arquivo de solução: `API_GHOST.sln`.

## Configuração

### Banco de dados

`Database:Target` escolhe qual entrada de `ConnectionStrings` a API usa (sem ele, vale `DefaultConnection`):

| Target | Banco | Onde |
|---|---|---|
| `Remote` | `sql.bsite.net` | padrão do `appsettings.json` |
| `Express` | SQL Server Express (`localhost,1433`, banco `AgendaGhost`, login do Windows) | padrão em Development |

Para trocar sem mexer no arquivo: variável de ambiente `Database__Target=Remote` (ou `Express`).

**SQL Server Express local ou numa VM.** Rode, como administrador, `infra/sqlserver/windows/instalar-sqlexpress.ps1`.
Ele baixa a mídia oficial, instala a instância `SQLEXPRESS` na porta 1433, abre o firewall, cria o banco `AgendaGhost` e
o login `ghost_api` e grava as connection strings (local e remota, com a senha) em `%USERPROFILE%\.api_ghost\sqlexpress.txt`,
fora do repositório. Numa VM na nuvem, abra também a porta 1433 no grupo de segurança e, na máquina da API, defina
`ConnectionStrings__Express` com a string remota desse arquivo.

**Importante:** não commite senhas ou chaves reais. Use [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) no desenvolvimento:

```bash
cd src/Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Express" "SUA_CONNECTION_STRING"
```

O log de inicialização mostra qual banco foi usado (`Banco: Express: servidor localhost,1433, banco AgendaGhost (Windows)`), sem senha.

### JWT

Na seção `Jwt` do `appsettings` (ou secrets / ambiente), configure pelo menos:

- `Key` — segredo simétrico com tamanho adequado para HS256 (mínimo exigido pela aplicação: chave suficientemente longa; troque qualquer valor padrão de exemplo).
- `Issuer` e `Audience` — devem coincidir com a validação configurada em `Program.cs`.
- `ExpiresMinutes` — tempo de vida do token.

Exemplo de estrutura (valores ilustrativos):

```json
"Jwt": {
  "Key": "SUBSTITUA_POR_SEGREDO_LONGO_E_ALEATORIO",
  "Issuer": "API_GHOST",
  "Audience": "API_GHOST",
  "ExpiresMinutes": 120
}
```

Na inicialização, a aplicação aplica migrações pendentes e executa seed de dados quando aplicável (`Program.cs`).

## Como executar

Na raiz do repositório:

```bash
dotnet restore
dotnet run --project src/Api
```

A URL e a porta aparecem no console (perfil `http` ou `https` conforme `launchSettings.json`).

Em ambiente **Development**, o Swagger UI fica disponível em `/swagger`.

## Autenticação

1. `POST /api/auth/login` com `username` e `password` (endpoint anônimo).
2. Nos demais endpoints, envie o JWT no cabeçalho `Authorization` como `Bearer <token>` ou apenas o valor do token, conforme configurado no projeto.
3. `POST /api/auth/renovar` troca um token ainda válido por outro com validade nova (o app da agenda usa para não deslogar no meio do dia).

No Swagger, use o esquema de segurança indicado na UI (cole o token retornado pelo login).

## Agenda da estética

Rotas em `api/agenda` (todas com JWT), usadas pelo app Flutter *Agenda da Estética*:

- `GET api/agenda/filiais`: filiais que o usuário pode abrir (sem vínculo em `UserCompanyBranches` = todas);
- `api/agenda/{filialId}/...`: configuração, agendamentos, disponibilidade, bloqueios, lembretes e clientes. O contrato
  está no `docs/contrato-api.md` do app. Erros de regra voltam como `{ "mensagem": "..." }` com 400, 403, 404 ou 409.

A reserva é atômica: a transação pega um lock nomeado do dia da filial (`sp_getapplock`) e revalida o horário, e o
lembrete D-1 entra na mesma transação. O `ReminderWorker` envia os lembretes vencidos a cada minuto
(`Agenda:ReminderWorker`). Sem WhatsApp configurado, o envio só é registrado no log (`LocalReminderSender`). As datas da
agenda são do fuso `Agenda:TimeZone` (padrão `America/Sao_Paulo`), então a API pode rodar num servidor em UTC.

Para o app no celular alcançar a API pela rede, use o perfil `rede` (escuta em todas as interfaces, IPv4 e IPv6, na porta 5000):

```bash
dotnet run --project src/Api --launch-profile rede
```

O Windows pode pedir para liberar a porta 5000 no firewall. Para o app na web, `Cors:Origins` lista as origens permitidas
(em Development qualquer origem é aceita).

Com a API no ar, `python infra/e2e/agenda_e2e.py` passa pelas rotas da agenda contra o banco de verdade: login,
renovação, filiais, ajustes, disponibilidade, 6 reservas simultâneas no mesmo horário (1 vira 201, 5 viram 409),
bloqueio, lembrete com envio local, resposta, remarcação, clientes e erros 400/401/403/404. Apaga o que cria.

## Documentação dos endpoints

Há uma referência estática em HTML (tema claro, formato pensado para leitura e PDF):

- [`docs/api-endpoints.html`](docs/api-endpoints.html)

Abra o arquivo no navegador ou use **Imprimir → Salvar como PDF** para exportar.

## Migrações (EF Core)

As migrações ficam em `src/Infrastructure/Persistence/Migrations`. Para criar o banco ou atualizar o esquema manualmente (além do `MigrateAsync` na subida da API):

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Para adicionar uma nova migração após alterar o modelo:

```bash
dotnet ef migrations add NomeDaMigracao --project src/Infrastructure --startup-project src/Api
```

É necessário ter a ferramenta global `dotnet-ef` instalada, se ainda não tiver:

```bash
dotnet tool install --global dotnet-ef
```

## Testes e build

```bash
dotnet build API_GHOST.sln
```

Inclua testes automatizados no projeto quando existirem; hoje o foco da documentação é build e execução da API.

## Licença

Defina a licença do repositório conforme a política da sua organização.
