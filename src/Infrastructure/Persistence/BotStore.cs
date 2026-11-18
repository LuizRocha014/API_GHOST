using Application.Agenda;
using Application.Bot;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Sessões, ids processados e conversa do bot. Usa conexão própria em cada operação: o lembrete é enviado
/// dentro da transação da agenda, e registrar a mensagem nela (ou depender dela) não faz sentido.
/// </summary>
public sealed class BotStore(IConfiguration configuration) : IBotStore
{
    private const int DuplicateKey = 2627;
    private const int DuplicateIndex = 2601;
    private const int MaxBody = 4096;

    private readonly string _connectionString = DatabaseConnection.Resolve(configuration);

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<bool> TryRegisterMessageAsync(string messageId, DateTime receivedAtUtc, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO BotProcessedMessages (MessageId, ReceivedAt)
            SELECT @MessageId, @ReceivedAt
            WHERE NOT EXISTS (SELECT 1 FROM BotProcessedMessages WITH (UPDLOCK, HOLDLOCK) WHERE MessageId = @MessageId)
            """;
        await using var conn = await OpenAsync(ct);
        try
        {
            var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { MessageId = Cut(messageId, 128), ReceivedAt = receivedAtUtc }, cancellationToken: ct));
            return rows == 1;
        }
        catch (SqlException ex) when (ex.Number is DuplicateKey or DuplicateIndex)
        {
            return false;
        }
    }

    public async Task<BotSession?> GetSessionAsync(Guid branchId, string phone, CancellationToken ct = default)
    {
        const string sql = "SELECT DataJson FROM BotSessions WHERE BranchId = @BranchId AND Phone = @Phone AND Active = 1";
        await using var conn = await OpenAsync(ct);
        var json = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, new { BranchId = branchId, Phone = phone }, cancellationToken: ct));
        return json is null ? null : BotSession.FromJson(json, phone);
    }

    public async Task SaveSessionAsync(Guid branchId, BotSession session, CancellationToken ct = default)
    {
        const string sql = """
            MERGE BotSessions WITH (HOLDLOCK) AS t
            USING (SELECT @BranchId AS BranchId, @Phone AS Phone) AS s
               ON t.BranchId = s.BranchId AND t.Phone = s.Phone
            WHEN MATCHED THEN
                UPDATE SET State = @State, DataJson = @DataJson, UpdatedAt = @Now, Active = 1
            WHEN NOT MATCHED THEN
                INSERT (Id, BranchId, Phone, State, DataJson, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @BranchId, @Phone, @State, @DataJson, @Now, @Now, 1);
            """;
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            session.Phone,
            session.State,
            DataJson = session.ToJson(),
            Now = DateTime.UtcNow,
        }, cancellationToken: ct));
    }

    public async Task LogMessageAsync(Guid branchId, string phone, string direction, string kind, string body, string? wamId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO BotMessages (Id, BranchId, Phone, Direction, Kind, Body, WamId, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @BranchId, @Phone, @Direction, @Kind, @Body, @WamId, @Now, @Now, 1)
            """;
        var now = DateTime.UtcNow;
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Phone = Cut(phone, AgendaService.MaxPhoneLength),
            Direction = Cut(direction, 8),
            Kind = Cut(kind, 16),
            Body = Cut(body, MaxBody),
            WamId = wamId is null ? null : Cut(wamId, 128),
            Now = now,
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<BotLogEntry>> ListMessagesAsync(Guid branchId, IReadOnlyList<string> phones, int limit, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Phone, Direction, Kind, Body, WamId, CreatedAt FROM (
                SELECT TOP (@Limit) Phone, Direction, Kind, Body, WamId, CreatedAt
                FROM BotMessages
                WHERE BranchId = @BranchId AND Phone IN @Phones AND Active = 1
                ORDER BY CreatedAt DESC
            ) x
            ORDER BY CreatedAt
            """;
        await using var conn = await OpenAsync(ct);
        var rows = await conn.QueryAsync<BotLogEntry>(new CommandDefinition(sql,
            new { BranchId = branchId, Phones = phones, Limit = Math.Clamp(limit, 1, 500) }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> PurgeAsync(DateTime processedBeforeUtc, DateTime messagesBeforeUtc, CancellationToken ct = default)
    {
        // Sessão parada há tanto tempo quanto a conversa também sai: guarda telefone e nome do cliente.
        const string sql = """
            DELETE FROM BotProcessedMessages WHERE ReceivedAt < @Processed;
            DECLARE @n INT = @@ROWCOUNT;
            DELETE FROM BotMessages WHERE CreatedAt < @Messages;
            SET @n = @n + @@ROWCOUNT;
            DELETE FROM BotSessions WHERE UpdatedAt < @Messages;
            SELECT @n + @@ROWCOUNT;
            """;
        await using var conn = await OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Processed = processedBeforeUtc, Messages = messagesBeforeUtc }, cancellationToken: ct));
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
