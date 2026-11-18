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

    public async Task LogMessageAsync(
        Guid branchId, string phone, string direction, string kind, string body, string? wamId, BotLogExtra? extra = null, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO BotMessages (Id, BranchId, Phone, Direction, Kind, Body, WamId, Meta, Author, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @BranchId, @Phone, @Direction, @Kind, @Body, @WamId, @Meta, @Author, @Now, @Now, 1)
            """;
        var args = new DynamicParameters(new
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Phone = Cut(phone, AgendaService.MaxPhoneLength),
            Direction = Cut(direction, 8),
            Kind = Cut(kind, 16),
            Body = Cut(body, MaxBody),
            WamId = wamId is null ? null : Cut(wamId, 128),
            Meta = extra?.Meta,
            Author = extra?.Author is null ? null : Cut(extra.Author, 80),
        });
        // datetime2: o Dapper mandaria datetime (3 ms) e a resposta do bot empataria com a mensagem do cliente na ordem.
        args.Add("Now", DateTime.UtcNow, System.Data.DbType.DateTime2);
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<BotLogEntry>> ListMessagesAsync(Guid branchId, IReadOnlyList<string> phones, int limit, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Phone, Direction, Kind, Body, WamId, CreatedAt, Meta, Author FROM (
                SELECT TOP (@Limit) Phone, Direction, Kind, Body, WamId, CreatedAt, Meta, Author
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

    public async Task<IReadOnlyList<BotConversationRow>> ListConversationsAsync(Guid branchId, DateTime sinceUtc, int limit, CancellationToken ct = default)
    {
        const string sql = """
            WITH conv AS (
                SELECT TOP (@Limit) Phone, MAX(CreatedAt) AS LastAt
                  FROM BotMessages
                 WHERE BranchId = @BranchId AND Active = 1 AND CreatedAt >= @Since
                 GROUP BY Phone
                 ORDER BY MAX(CreatedAt) DESC
            )
            SELECT c.Phone, c.LastAt,
                   l.Direction AS LastDirection, l.Kind AS LastKind, l.Body AS LastBody,
                   (SELECT MAX(i.CreatedAt) FROM BotMessages i
                     WHERE i.BranchId = @BranchId AND i.Phone = c.Phone AND i.Direction = 'in' AND i.Active = 1) AS LastInAt,
                   (SELECT COUNT(1) FROM BotMessages u
                     WHERE u.BranchId = @BranchId AND u.Phone = c.Phone AND u.Direction = 'in' AND u.Active = 1
                       AND u.CreatedAt > ISNULL(s.SeenAt, '19000101')) AS Unread,
                   s.State, s.DataJson
              FROM conv c
             OUTER APPLY (SELECT TOP 1 Direction, Kind, Body FROM BotMessages m
                           WHERE m.BranchId = @BranchId AND m.Phone = c.Phone AND m.Active = 1
                           ORDER BY m.CreatedAt DESC) l
              LEFT JOIN BotSessions s ON s.BranchId = @BranchId AND s.Phone = c.Phone AND s.Active = 1
             ORDER BY c.LastAt DESC
            """;
        await using var conn = await OpenAsync(ct);
        var rows = await conn.QueryAsync<BotConversationRow>(new CommandDefinition(sql,
            new { BranchId = branchId, Since = sinceUtc, Limit = limit }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task MarkSeenAsync(Guid branchId, string phone, DateTime seenAtUtc, CancellationToken ct = default)
    {
        const string sql = "UPDATE BotSessions SET SeenAt = @SeenAt WHERE BranchId = @BranchId AND Phone = @Phone";
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { BranchId = branchId, Phone = phone, SeenAt = seenAtUtc }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<BotClientName>> ClientNamesAsync(Guid branchId, IReadOnlyList<string> phones, CancellationToken ct = default)
    {
        if (phones.Count == 0) return [];
        const string sql = """
            SELECT CONVERT(varchar(36), Id) AS Id, Phone, Name FROM AgendaClients
             WHERE BranchId = @BranchId AND Active = 1 AND Phone IN @Phones
            """;
        await using var conn = await OpenAsync(ct);
        var rows = await conn.QueryAsync<(string Id, string Phone, string Name)>(new CommandDefinition(sql,
            new { BranchId = branchId, Phones = phones }, cancellationToken: ct));
        return rows.Select(r => new BotClientName(r.Id.ToLowerInvariant(), r.Phone, r.Name)).ToList();
    }

    public async Task<int> PurgeAsync(DateTime processedBeforeUtc, DateTime messagesBeforeUtc, CancellationToken ct = default)
    {
        // Sessão parada há tanto tempo quanto a conversa também sai: guarda telefone e nome do cliente.
        // Da fila: o que foi processado sai junto com os ids (7 dias); o que falhou fica até a retenção da conversa.
        const string sql = """
            DELETE FROM BotProcessedMessages WHERE ReceivedAt < @Processed;
            DECLARE @n INT = @@ROWCOUNT;
            DELETE FROM BotMessages WHERE CreatedAt < @Messages;
            SET @n = @n + @@ROWCOUNT;
            DELETE FROM BotSessions WHERE UpdatedAt < @Messages;
            SET @n = @n + @@ROWCOUNT;
            DELETE FROM BotQueueItems WHERE (Status = 'done' AND ReceivedAt < @Processed) OR ReceivedAt < @Messages;
            SELECT @n + @@ROWCOUNT;
            """;
        await using var conn = await OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Processed = processedBeforeUtc, Messages = messagesBeforeUtc }, cancellationToken: ct));
    }

    /* ---------- Fila do webhook ---------- */

    private const string QueueColumns =
        "Id, BranchId, Kind, ExternalId, PayloadJson, Status, Attempts, ReceivedAt, NextAttemptAt, ProcessedAt, Error";

    public async Task<bool> EnqueueAsync(Domain.Agenda.BotQueueItem item, CancellationToken ct = default)
    {
        const string sql = $"""
            INSERT INTO BotQueueItems ({QueueColumns})
            SELECT @Id, @BranchId, @Kind, @ExternalId, @PayloadJson, @Status, @Attempts, @ReceivedAt, @NextAttemptAt, @ProcessedAt, @Error
            WHERE @ExternalId IS NULL
               OR NOT EXISTS (SELECT 1 FROM BotQueueItems WITH (UPDLOCK, HOLDLOCK) WHERE ExternalId = @ExternalId)
            """;
        var args = new DynamicParameters(new
        {
            item.Id,
            item.BranchId,
            Kind = Cut(item.Kind, 16),
            ExternalId = item.ExternalId is null ? null : Cut(item.ExternalId, 160),
            item.PayloadJson,
            item.Status,
            item.Attempts,
            item.ProcessedAt,
            item.Error,
        });
        // datetime2: a ordem de chegada decide a ordem de processamento.
        args.Add("ReceivedAt", item.ReceivedAt, System.Data.DbType.DateTime2);
        args.Add("NextAttemptAt", item.NextAttemptAt, System.Data.DbType.DateTime2);
        await using var conn = await OpenAsync(ct);
        try
        {
            return await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct)) == 1;
        }
        catch (SqlException ex) when (ex.Number is DuplicateKey or DuplicateIndex)
        {
            return false;
        }
    }

    public async Task<Domain.Agenda.BotQueueItem?> ClaimNextAsync(DateTime nowUtc, DateTime leaseUntilUtc, CancellationToken ct = default)
    {
        // READPAST: com mais de um worker (duas instâncias da API), cada um pega um item diferente.
        const string sql = """
            WITH next AS (
                SELECT TOP (1) * FROM BotQueueItems WITH (UPDLOCK, READPAST, ROWLOCK)
                 WHERE Status IN ('pending', 'processing') AND NextAttemptAt <= @Now
                 ORDER BY ReceivedAt
            )
            UPDATE next SET Status = 'processing', Attempts = Attempts + 1, NextAttemptAt = @LeaseUntil
            OUTPUT inserted.Id, inserted.BranchId, inserted.Kind, inserted.ExternalId, inserted.PayloadJson, inserted.Status,
                   inserted.Attempts, inserted.ReceivedAt, inserted.NextAttemptAt, inserted.ProcessedAt, inserted.Error;
            """;
        var args = new DynamicParameters();
        args.Add("Now", nowUtc, System.Data.DbType.DateTime2);
        args.Add("LeaseUntil", leaseUntilUtc, System.Data.DbType.DateTime2);
        await using var conn = await OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Domain.Agenda.BotQueueItem>(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public async Task CompleteAsync(Guid id, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE BotQueueItems SET Status = 'done', ProcessedAt = @Now, Error = NULL WHERE Id = @Id",
            new { Id = id, Now = nowUtc }, cancellationToken: ct));
    }

    public async Task RetryLaterAsync(Guid id, string error, DateTime nextAttemptUtc, bool giveUp, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE BotQueueItems SET Status = @Status, Error = @Error, NextAttemptAt = @Next WHERE Id = @Id",
            new { Id = id, Status = giveUp ? "failed" : "pending", Error = Cut(error, 1000), Next = nextAttemptUtc }, cancellationToken: ct));
    }

    public async Task<BotQueueHealth> QueueHealthAsync(DateTime failedSinceUtc, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(1) FROM BotQueueItems WHERE Status IN ('pending', 'processing')) AS Pending,
                (SELECT COUNT(1) FROM BotQueueItems WHERE Status = 'failed' AND ReceivedAt >= @Since) AS Failed,
                (SELECT MIN(ReceivedAt) FROM BotQueueItems WHERE Status IN ('pending', 'processing')) AS OldestPendingAt
            """;
        await using var conn = await OpenAsync(ct);
        var row = await conn.QuerySingleAsync<(int Pending, int Failed, DateTime? OldestPendingAt)>(
            new CommandDefinition(sql, new { Since = failedSinceUtc }, cancellationToken: ct));
        return new BotQueueHealth(row.Pending, row.Failed, row.OldestPendingAt);
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
