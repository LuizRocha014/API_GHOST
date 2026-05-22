# GHOST — SQL Migrations

Coloque aqui scripts `.sql` (embedded como recurso) que serão aplicados pelo `MigrationRunner` na ordem alfabética do nome do arquivo.

Padrão recomendado: `NNN_DescricaoCurta.sql` (ex.: `001_InitialSchema.sql`, `002_AddUserBranchAccess.sql`).

Regras:
- Cada arquivo só roda uma vez (controle via tabela `HistoryMigration`).
- Use `GO` para separar batches do T-SQL.
- Scripts devem ser idempotentes (`IF OBJECT_ID(...) IS NULL`, `IF NOT EXISTS (...)`).
- A flag `Database:RunMigrations` em `appsettings.json` controla se o runner roda no startup.

Observação: o GHOST já possui migrations EF Core em `Persistence/Migrations/`. Elas continuam funcionando — o runner SQL adicional aqui é para mudanças que você prefere escrever direto em T-SQL (ex.: stored procedures, views, índices, dados de referência).
