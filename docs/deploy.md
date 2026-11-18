# Publicar a Agenda da Estética

Como colocar a API (com o app web junto) num endereço https público, para o WhatsApp da Meta conseguir chamar o
webhook e a equipe usar o app de qualquer lugar. Três caminhos, do mais recomendado ao mais caseiro:

| Caminho | Custo aproximado | Para quem |
| --- | --- | --- |
| **A. VPS com Docker** (Hostinger, Contabo, DigitalOcean, Hetzner, Oracle Cloud grátis) | R$ 25–60/mês | Recomendado: tudo pronto em `deploy/` (banco, API, app web, HTTPS automático e backup). |
| **B. Azure App Service + Azure SQL** | R$ 70–150/mês | Quem prefere não cuidar de servidor. |
| **C. Computador da estética + Cloudflare Tunnel** | grátis | Começar sem gastar; depende do PC ligado e da internet do local. |

Em qualquer caminho:

- a API sobe sozinha, cria o banco e aplica as migrações (`MigrateAsync`);
- no banco vazio cria a empresa e a filial (`Seed:Empresa`, `Seed:Filial`) e o usuário `admin` com a senha padrão
  do seed, que **é obrigado a trocar no primeiro acesso** (fora do Development, `Seed:ForcarTrocaDaSenhaPadrao`);
- `GET /health` responde `200` com o banco e a fila do bot (serve para monitorar, por exemplo no UptimeRobot).

## A. VPS com Docker

Arquivos em [`deploy/`](../deploy): `docker-compose.yml` (SQL Server Express, API e Caddy), `Caddyfile` (HTTPS),
`.env.example` (segredos), `publicar-web.ps1` (gera o app web) e `backup/`.

1. **Servidor:** Ubuntu 24.04 com 2 GB de RAM ou mais (o SQL Server pede 2 GB). Instale o Docker:
   `curl -fsSL https://get.docker.com | sh`.
2. **Domínio:** crie um registro **A** (ex.: `agenda.suaestetica.com.br`) apontando para o IP do servidor. Libere as
   portas 80 e 443 no firewall do provedor.
3. **Código:** copie o repositório da API para o servidor (ex.: `/opt/agenda`), com `git clone` ou `scp`.
4. **Segredos:** em `deploy/`, `cp .env.example .env` e preencha `DOMINIO`, `SQL_SENHA` (forte) e `JWT_CHAVE`
   (64 caracteres aleatórios: `openssl rand -base64 48`). O `.env` não vai para o git.
5. **App web:** no seu PC, `deploy\publicar-web.ps1` gera o app e copia para `deploy/web`; mande essa pasta para o
   servidor (`scp -r deploy/web usuario@servidor:/opt/agenda/deploy/`). O app no navegador usa o próprio endereço do
   site como servidor.
6. **Subir:** `cd /opt/agenda/deploy && docker compose up -d --build`. Acompanhe com `docker compose logs -f api`:
   aparecem o banco, o modo do WhatsApp e "App web servido de /app/web".
7. Abra `https://<domínio>`: entre com `admin` e a senha padrão do seed; o app pede a senha nova.

Atualizar: `git pull` e `docker compose up -d --build` (a API aplica as migrações novas ao subir). Atualizar só o app
web: rode `publicar-web.ps1`, copie a pasta `web` de novo e pronto (não precisa reiniciar nada).

### Backup no Docker

```bash
cd /opt/agenda/deploy
mkdir -p backups && sudo chown 10001:0 backups   # o SQL Server do container roda com o usuário 10001
sh backup/backup-docker.sh                       # teste: grava backups/AgendaGhost-<data>.bak conferido
crontab -e                                       # todo dia às 3h:
# 0 3 * * * cd /opt/agenda/deploy && sh backup/backup-docker.sh >> backups/backup.log 2>&1
```

Backup só no mesmo servidor não protege de perder o servidor. Copie a pasta `backups` para fora, por exemplo com o
[rclone](https://rclone.org) para o Google Drive (`rclone copy backups gdrive:agenda-backups` no mesmo cron).

## B. Azure App Service + Azure SQL

1. Crie um **Banco de Dados SQL do Azure** (camada Basic ou serverless) e anote a connection string.
2. Crie um **App Service** Linux com a pilha **.NET 10**. Em Configuração → Variáveis de ambiente:
   `Database__Target=Remote`, `ConnectionStrings__Remote=<connection string>`, `Jwt__Key=<64 caracteres>`,
   `Web__Path=/home/site/wwwroot/web`, `Seed__DemoData=false`, `Seed__Empresa`, `Seed__Filial` e as `WhatsApp__*`.
   Ligue **Sempre ativado** (o bot e os lembretes rodam em segundo plano).
3. Publique: `dotnet publish src/Api -c Release -o publish`, copie o build web do app para `publish/web` e envie a
   pasta (`az webapp deploy` ou o Visual Studio). O HTTPS e o domínio próprio são configurados no portal.
4. O Azure SQL tem backup automático (7 a 35 dias).

## C. Computador da estética + Cloudflare Tunnel

1. Instale o SQL Server Express (`infra/sqlserver/windows/instalar-sqlexpress.ps1`) e o .NET 10.
2. Publique a API numa pasta (`dotnet publish src/Api -c Release -o C:\Agenda\api`), copie o build web do app para
   `C:\Agenda\api\web` e rode como serviço do Windows (`sc.exe create` ou o [NSSM](https://nssm.cc)) com as variáveis
   `ASPNETCORE_URLS=http://localhost:5000`, `Database__Target=Express`, `Jwt__Key`, `Web__Path=web`, `Proxy__Forwarded=true`.
3. Com uma conta grátis da Cloudflare e o domínio nela, crie um **túnel nomeado** (`cloudflared tunnel create agenda`)
   apontando `agenda.suaestetica.com.br` para `http://localhost:5000` e instale o `cloudflared` como serviço. O endereço
   fica fixo e com HTTPS, sem abrir portas no roteador.
4. Backup diário: `deploy\backup\registrar-tarefa.ps1 -CopiarPara "<pasta do OneDrive/Google Drive>"` (como
   administrador). A tarefa roda como SYSTEM: dê a ela acesso ao SQL Server uma vez, no `sqlcmd` como administrador:
   `CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];`
   (ou use `-Usuario/-Senha` de um login SQL com permissão de backup).

## Restaurar um backup

Com a API parada:

```sql
-- Windows (sqlcmd -S localhost,1433 -E) ou Docker (docker compose exec db /opt/mssql-tools18/bin/sqlcmd -C -U sa -P ...)
RESTORE DATABASE AgendaGhost FROM DISK = N'<caminho>\AgendaGhost-20261118-233000.bak' WITH REPLACE;
```

Teste a restauração de vez em quando num banco com outro nome (`WITH MOVE`), para ter certeza de que o backup serve.

## Depois de publicar

1. **WhatsApp:** siga [whatsapp-bot.md](whatsapp-bot.md#ligar-na-meta-passo-a-passo) com a URL definitiva
   `https://<domínio>/api/whatsapp/webhook` e cadastre os modelos (lista em `GET api/agenda/{filial}/bot/modelos`).
2. **App no celular:** na tela de login, em Servidor, `https://<domínio>`. Para distribuir o APK assinado, veja o README
   do app (chave de assinatura em `android/key.properties`).
3. **Equipe:** em Ajustes › Equipe e filiais, cadastre os funcionários (perfil Equipe) com senha provisória.
4. **Monitoramento:** cadastre `https://<domínio>/health` num monitor grátis (UptimeRobot, Better Stack) para ser avisado
   se a API ou o banco caírem.

## Checklist de segurança

- [ ] `Jwt__Key` e a senha do banco novas, longas e só no servidor (nunca no git).
- [ ] O usuário `admin` trocou a senha padrão (o app obriga) e cada funcionário tem login próprio.
- [ ] `WhatsApp__AppSecret` configurado (sem ele, o webhook recusa tudo em produção).
- [ ] Só as portas 80/443 abertas; o SQL Server não fica exposto na internet.
- [ ] Backup diário rodando e copiado para fora do servidor; restauração testada.
- [ ] Se algum segredo já esteve num repositório público, troque-o (senha do banco antigo, tokens).
