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

Defina a string de conexão `ConnectionStrings:DefaultConnection` (por exemplo em `src/Api/appsettings.Development.json` ou variáveis de ambiente).

**Importante:** não commite senhas ou chaves reais. Use [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) no desenvolvimento:

```bash
cd src/Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "SUA_CONNECTION_STRING"
```

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

No Swagger, use o esquema de segurança indicado na UI (cole o token retornado pelo login).

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
