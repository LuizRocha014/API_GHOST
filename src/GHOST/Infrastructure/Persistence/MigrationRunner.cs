using System.Reflection;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Aplica migrations SQL (embedded resources em Migrations/Sql/*.sql) controladas
/// pela tabela `HistoryMigration`. Cada script só é executado uma vez.
/// </summary>
public sealed class MigrationRunner
{
    private const string ConnectionName = "DefaultConnection";
    private const string ResourceFolder = "Migrations.Sql";

    private readonly IConfiguration _configuration;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IConfiguration configuration, ILogger<MigrationRunner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var cs = _configuration.GetConnectionString(ConnectionName)
                 ?? throw new InvalidOperationException(
                     $"ConnectionStrings:{ConnectionName} não configurado.");

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await EnsureHistoryTableAsync(conn, cancellationToken).ConfigureAwait(false);
        var executed = await GetExecutedAsync(conn, cancellationToken).ConfigureAwait(false);

        var scripts = LoadEmbeddedScripts()
            .Where(s => !executed.Contains(s.Name))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scripts.Count == 0)
        {
            _logger.LogInformation("Nenhuma migration SQL pendente.");
            return;
        }

        foreach (var script in scripts)
        {
            _logger.LogInformation("Aplicando migration {Name}…", script.Name);
            await ExecuteScriptAsync(conn, script.Sql, cancellationToken).ConfigureAwait(false);
            await RecordAsync(conn, script.Name, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Migration {Name} aplicada.", script.Name);
        }
    }

    private static async Task EnsureHistoryTableAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.HistoryMigration', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.HistoryMigration
                (
                    id           INT             NOT NULL IDENTITY(1,1) CONSTRAINT PK_HistoryMigration PRIMARY KEY,
                    name         NVARCHAR(255)   NOT NULL,
                    executed_at  DATETIME2(3)    NOT NULL CONSTRAINT DF_HistoryMigration_executed DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT UQ_HistoryMigration_name UNIQUE (name)
                );
            END
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
    }

    private static async Task<HashSet<string>> GetExecutedAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = "SELECT name FROM dbo.HistoryMigration";
        var names = await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<MigrationScript> LoadEmbeddedScripts()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var assemblyName = assembly.GetName().Name ?? string.Empty;
        var prefix = $"{assemblyName}.{ResourceFolder}.";

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Não foi possível ler o recurso embedded {resource}.");
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            var name = resource[prefix.Length..^4];
            yield return new MigrationScript(name, sql);
        }
    }

    private static async Task ExecuteScriptAsync(SqlConnection conn, string sql, CancellationToken ct)
    {
        foreach (var batch in SplitOnGo(sql))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = batch;
            cmd.CommandTimeout = 300;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> SplitOnGo(string sql)
    {
        var current = new StringBuilder();
        foreach (var line in sql.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                yield return current.ToString();
                current.Clear();
            }
            else
            {
                current.AppendLine(line.TrimEnd('\r'));
            }
        }
        if (current.Length > 0)
            yield return current.ToString();
    }

    private static async Task RecordAsync(SqlConnection conn, string name, CancellationToken ct)
    {
        const string sql = "INSERT INTO dbo.HistoryMigration (name) VALUES (@Name)";
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Name = name }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed record MigrationScript(string Name, string Sql);
}
