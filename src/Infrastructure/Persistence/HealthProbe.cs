using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>Situação da API para o /health: banco, fila do webhook e mensagens que falharam.</summary>
public sealed record HealthReport(
    string Status,
    bool Database,
    int QueuePending,
    int QueueFailed24h,
    int? OldestPendingSeconds,
    int RemindersFailed24h,
    string? Error);

/// <summary>
/// Confere o banco e o que está parado ou falhou nas últimas 24 h. "ok" = tudo em dia; "atencao" = há falha de envio ou
/// fila atrasada (mais de 2 min); "fora" = sem banco.
/// </summary>
public sealed class HealthProbe(IConfiguration configuration)
{
    private static readonly TimeSpan Late = TimeSpan.FromMinutes(2);
    private readonly string _connectionString = DatabaseConnection.Resolve(configuration);

    public async Task<HealthReport> CheckAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(1) FROM BotQueueItems WHERE Status IN ('pending', 'processing')) AS QueuePending,
                (SELECT COUNT(1) FROM BotQueueItems WHERE Status = 'failed' AND ReceivedAt >= @Since) AS QueueFailed,
                (SELECT MIN(ReceivedAt) FROM BotQueueItems WHERE Status IN ('pending', 'processing')) AS OldestPending,
                (SELECT COUNT(1) FROM AgendaReminders WHERE Status = 'failed' AND UpdatedAt >= @Since AND Active = 1) AS RemindersFailed
            """;
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var now = DateTime.UtcNow;
            var row = await conn.QuerySingleAsync<(int QueuePending, int QueueFailed, DateTime? OldestPending, int RemindersFailed)>(
                new CommandDefinition(sql, new { Since = now.AddHours(-24) }, cancellationToken: ct, commandTimeout: 10));
            int? oldest = row.OldestPending is { } at ? (int)Math.Max(0, (now - at).TotalSeconds) : null;
            var attention = row.QueueFailed > 0 || row.RemindersFailed > 0 || oldest > Late.TotalSeconds;
            return new HealthReport(attention ? "atencao" : "ok", true, row.QueuePending, row.QueueFailed, oldest, row.RemindersFailed, null);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or TimeoutException)
        {
            return new HealthReport("fora", false, 0, 0, null, 0, "Banco de dados indisponível.");
        }
    }
}
