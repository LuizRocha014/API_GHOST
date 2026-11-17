using System.Data;
using System.Data.Common;
using Application.Abstractions;
using Application.Agenda;
using Dapper;
using Domain.Agenda;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence;

public sealed class AgendaRepository(SqlSession session) : IAgendaRepository
{
    private SqlConnection Conn => session.Connection;

    internal const string BookingColumns = """
        Id, BranchId, [Date], StartMin, EndMin, BoxId, ServiceId, ServiceName, Price, VehicleType, Vehicle, Plate,
        ClientName, Phone, Notes, Status, Origin, RescheduledFrom, CreatedAt, UpdatedAt, Active
        """;

    internal const string ReminderColumns = """
        Id, BranchId, BookingId, Type, ScheduledFor, Status, Attempts, WamId, Answer, AnsweredAt, Error, CreatedAt, UpdatedAt, Active
        """;

    internal const string ClientColumns = "Id, BranchId, Phone, Name, ReminderOptIn, OptInAt, CreatedAt, UpdatedAt, Active";

    /* ---------- Acesso ---------- */

    public async Task<IReadOnlyList<AgendaBranchDto>> ListBranchesForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Usuário sem vínculo nenhum com filiais (ex.: admin do seed) enxerga todas as filiais ativas.
        const string sql = """
            IF EXISTS (SELECT 1 FROM UserCompanyBranches WHERE UserId = @UserId AND Active = 1)
                SELECT DISTINCT b.Id, b.Name, b.CompanyId, c.Name AS CompanyName
                FROM UserCompanyBranches ucb
                INNER JOIN Branches b ON b.Id = ucb.BranchId
                INNER JOIN Companies c ON c.Id = b.CompanyId
                WHERE ucb.UserId = @UserId AND ucb.Active = 1 AND b.Active = 1
                ORDER BY CompanyName, Name
            ELSE
                SELECT b.Id, b.Name, b.CompanyId, c.Name AS CompanyName
                FROM Branches b
                INNER JOIN Companies c ON c.Id = b.CompanyId
                WHERE b.Active = 1
                ORDER BY CompanyName, Name
            """;
        var rows = await Conn.QueryAsync<BranchRow>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return rows.Select(r => new AgendaBranchDto(r.Id, r.Name, r.CompanyId, r.CompanyName)).ToList();
    }

    private sealed class BranchRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }

    public async Task<bool> CanAccessBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT CASE
                WHEN NOT EXISTS (SELECT 1 FROM Branches WHERE Id = @BranchId AND Active = 1) THEN 0
                WHEN NOT EXISTS (SELECT 1 FROM UserCompanyBranches WHERE UserId = @UserId AND Active = 1) THEN 1
                WHEN EXISTS (SELECT 1 FROM UserCompanyBranches WHERE UserId = @UserId AND BranchId = @BranchId AND Active = 1) THEN 1
                ELSE 0
            END
            """;
        return await Conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { UserId = userId, BranchId = branchId }, cancellationToken: ct)) == 1;
    }

    public Task<string?> GetBranchNameAsync(Guid branchId, CancellationToken ct = default) =>
        Conn.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT Name FROM Branches WHERE Id = @Id AND Active = 1", new { Id = branchId }, cancellationToken: ct));

    /* ---------- Configuração ---------- */

    public Task<string?> GetConfigJsonAsync(Guid branchId, CancellationToken ct = default) =>
        Conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ConfigJson FROM AgendaSettings WHERE BranchId = @BranchId AND Active = 1", new { BranchId = branchId }, cancellationToken: ct));

    public async Task SaveConfigJsonAsync(Guid branchId, string json, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AgendaSettings WITH (UPDLOCK, HOLDLOCK)
               SET ConfigJson = @Json, UpdatedAt = @Utc, Active = 1
             WHERE BranchId = @BranchId;
            IF @@ROWCOUNT = 0
                INSERT INTO AgendaSettings (Id, BranchId, ConfigJson, CreatedAt, UpdatedAt, Active)
                VALUES (NEWID(), @BranchId, @Json, @Utc, @Utc, 1);
            """;
        await using var tx = await Conn.BeginTransactionAsync(ct);
        await Conn.ExecuteAsync(new CommandDefinition(sql, new { BranchId = branchId, Json = json, Utc = DateTime.UtcNow }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    /* ---------- Leitura ---------- */

    public async Task<IReadOnlyList<AgendaBooking>> ListBookingsAsync(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT {BookingColumns} FROM AgendaBookings
            WHERE BranchId = @BranchId AND Active = 1 AND [Date] BETWEEN @From AND @To
            ORDER BY [Date], StartMin, BoxId
            """;
        return (await Conn.QueryAsync<AgendaBooking>(new CommandDefinition(sql, new { BranchId = branchId, From = from.Date, To = to.Date }, cancellationToken: ct))).AsList();
    }

    public Task<AgendaBooking?> GetBookingAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        Conn.QuerySingleOrDefaultAsync<AgendaBooking?>(new CommandDefinition(
            $"SELECT {BookingColumns} FROM AgendaBookings WHERE Id = @Id AND BranchId = @BranchId AND Active = 1",
            new { Id = id, BranchId = branchId }, cancellationToken: ct));

    public async Task<IReadOnlyList<AgendaBlock>> ListBlocksAsync(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, BranchId, BoxId, [Date], StartMin, EndMin, Reason, CreatedAt, UpdatedAt, Active
            FROM AgendaBlocks
            WHERE BranchId = @BranchId AND Active = 1 AND [Date] BETWEEN @From AND @To
            ORDER BY [Date], StartMin
            """;
        return (await Conn.QueryAsync<AgendaBlock>(new CommandDefinition(sql, new { BranchId = branchId, From = from.Date, To = to.Date }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<AgendaReminder>> ListRemindersAsync(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.Id, r.BranchId, r.BookingId, r.Type, r.ScheduledFor, r.Status, r.Attempts, r.WamId, r.Answer, r.AnsweredAt, r.Error,
                   r.CreatedAt, r.UpdatedAt, r.Active
            FROM AgendaReminders r
            INNER JOIN AgendaBookings b ON b.Id = r.BookingId
            WHERE r.BranchId = @BranchId AND r.Active = 1 AND b.Active = 1 AND b.[Date] BETWEEN @From AND @To
            ORDER BY r.ScheduledFor
            """;
        return (await Conn.QueryAsync<AgendaReminder>(new CommandDefinition(sql, new { BranchId = branchId, From = from.Date, To = to.Date }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<Guid>> ListBranchesWithDueRemindersAsync(DateTime until, CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT BranchId
            FROM AgendaReminders
            WHERE Active = 1 AND Status = 'pending' AND ScheduledFor <= @Until
            """;
        return (await Conn.QueryAsync<Guid>(new CommandDefinition(sql, new { Until = until }, cancellationToken: ct))).AsList();
    }

    public Task<AgendaClient?> GetClientAsync(Guid branchId, IReadOnlyList<string> phoneVariants, CancellationToken ct = default) =>
        Conn.QueryFirstOrDefaultAsync<AgendaClient?>(new CommandDefinition(
            $"SELECT TOP (1) {ClientColumns} FROM AgendaClients WHERE BranchId = @BranchId AND Active = 1 AND Phone IN @Phones ORDER BY UpdatedAt DESC",
            new { BranchId = branchId, Phones = phoneVariants }, cancellationToken: ct));

    public async Task<IReadOnlyList<AgendaClient>> ListClientsAsync(Guid branchId, CancellationToken ct = default) =>
        (await Conn.QueryAsync<AgendaClient>(new CommandDefinition(
            $"SELECT {ClientColumns} FROM AgendaClients WHERE BranchId = @BranchId AND Active = 1 ORDER BY Name",
            new { BranchId = branchId }, cancellationToken: ct))).AsList();

    /* ---------- Escrita simples ---------- */

    public async Task<AgendaBlock> AddBlockAsync(AgendaBlock block, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO AgendaBlocks (Id, BranchId, BoxId, [Date], StartMin, EndMin, Reason, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @BranchId, @BoxId, @Date, @StartMin, @EndMin, @Reason, @CreatedAt, @UpdatedAt, @Active)
            """;
        await Conn.ExecuteAsync(new CommandDefinition(sql, block, cancellationToken: ct));
        return block;
    }

    public async Task<bool> DeleteBlockAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await Conn.ExecuteAsync(new CommandDefinition(
            "UPDATE AgendaBlocks SET Active = 0, UpdatedAt = @Utc WHERE Id = @Id AND BranchId = @BranchId AND Active = 1",
            new { Id = id, BranchId = branchId, Utc = DateTime.UtcNow }, cancellationToken: ct)) > 0;

    public async Task<bool> DeleteBookingAsync(Guid branchId, Guid id, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AgendaReminders SET Active = 0, UpdatedAt = @Utc WHERE BookingId = @Id AND BranchId = @BranchId;
            UPDATE AgendaBookings SET Active = 0, UpdatedAt = @Utc WHERE Id = @Id AND BranchId = @BranchId AND Active = 1;
            """;
        await using var tx = await Conn.BeginTransactionAsync(ct);
        var n = await Conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, BranchId = branchId, Utc = DateTime.UtcNow }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return n > 0 && await Conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM AgendaBookings WHERE Id = @Id AND BranchId = @BranchId", new { Id = id, BranchId = branchId }, cancellationToken: ct)) == 1;
    }

    public async Task<int> PruneBookingsAsync(Guid branchId, DateTime before, CancellationToken ct = default)
    {
        // Exclusão definitiva (LGPD: guardar só o necessário).
        const string sql = """
            DELETE r FROM AgendaReminders r
            INNER JOIN AgendaBookings b ON b.Id = r.BookingId
            WHERE b.BranchId = @BranchId AND b.[Date] < @Before;
            DELETE FROM AgendaBookings WHERE BranchId = @BranchId AND [Date] < @Before;
            SELECT @@ROWCOUNT;
            """;
        await using var tx = await Conn.BeginTransactionAsync(ct);
        var n = await Conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { BranchId = branchId, Before = before.Date }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return n;
    }

    /* ---------- Transações ---------- */

    public async Task<T> InDayTransactionAsync<T>(Guid branchId, DateTime date, Func<IAgendaDayTransaction, Task<T>> work, CancellationToken ct = default)
    {
        await using var tx = await Conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var t = new AgendaTransaction(Conn, tx, branchId, ct);
            await t.LockDayAsync(date.Date);
            var result = await work(t);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<T> InTransactionAsync<T>(Guid branchId, Func<IAgendaDayTransaction, Task<T>> work, CancellationToken ct = default)
    {
        await using var tx = await Conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await work(new AgendaTransaction(Conn, tx, branchId, ct));
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

internal sealed class AgendaTransaction(SqlConnection conn, DbTransaction tx, Guid branchId, CancellationToken ct) : IAgendaDayTransaction
{
    private DateTime? _lockedDate;
    private List<AgendaBooking>? _lockedBookings;

    private CommandDefinition Cmd(string sql, object? args = null) => new(sql, args, tx, cancellationToken: ct);

    private const int DayLockTimeoutMs = 15_000;

    /// <summary>
    /// Trava o dia da filial com um lock nomeado até o fim da transação: outra reserva ou remarcação
    /// do mesmo dia espera esta terminar, então a checagem de conflito e o INSERT ficam atômicos.
    /// UPDLOCK + HOLDLOCK na tabela travava faixas do índice e dava deadlock quando duas reservas
    /// chegavam juntas num dia com poucos agendamentos.
    /// </summary>
    public async Task LockDayAsync(DateTime date)
    {
        const string lockSql = """
            DECLARE @Result int;
            EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @Timeout;
            SELECT @Result;
            """;
        var resource = $"agenda:{branchId:N}:{date:yyyyMMdd}";
        var result = await conn.ExecuteScalarAsync<int>(Cmd(lockSql, new { Resource = resource, Timeout = DayLockTimeoutMs }));
        if (result < 0) throw new AgendaConflictException("A agenda deste dia está sendo alterada agora. Tente de novo em instantes.");

        var sql = $"SELECT {AgendaRepository.BookingColumns} FROM AgendaBookings WHERE BranchId = @BranchId AND [Date] = @Date AND Active = 1";
        _lockedBookings = (await conn.QueryAsync<AgendaBooking>(Cmd(sql, new { BranchId = branchId, Date = date }))).AsList();
        _lockedDate = date;
    }

    public async Task<IReadOnlyList<AgendaBooking>> DayBookingsAsync(DateTime date)
    {
        if (_lockedDate == date.Date && _lockedBookings is not null) return _lockedBookings;
        var sql = $"SELECT {AgendaRepository.BookingColumns} FROM AgendaBookings WHERE BranchId = @BranchId AND [Date] = @Date AND Active = 1";
        return (await conn.QueryAsync<AgendaBooking>(Cmd(sql, new { BranchId = branchId, Date = date.Date }))).AsList();
    }

    public async Task<IReadOnlyList<AgendaBlock>> DayBlocksAsync(DateTime date) =>
        (await conn.QueryAsync<AgendaBlock>(Cmd(
            "SELECT Id, BranchId, BoxId, [Date], StartMin, EndMin, Reason, CreatedAt, UpdatedAt, Active FROM AgendaBlocks WHERE BranchId = @BranchId AND [Date] = @Date AND Active = 1",
            new { BranchId = branchId, Date = date.Date }))).AsList();

    public Task<AgendaBooking?> GetBookingAsync(Guid id) =>
        conn.QuerySingleOrDefaultAsync<AgendaBooking?>(Cmd(
            $"SELECT {AgendaRepository.BookingColumns} FROM AgendaBookings WITH (UPDLOCK) WHERE Id = @Id AND BranchId = @BranchId AND Active = 1",
            new { Id = id, BranchId = branchId }));

    public async Task InsertBookingAsync(AgendaBooking b)
    {
        const string sql = """
            INSERT INTO AgendaBookings (Id, BranchId, [Date], StartMin, EndMin, BoxId, ServiceId, ServiceName, Price, VehicleType, Vehicle, Plate,
                                        ClientName, Phone, Notes, Status, Origin, RescheduledFrom, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @BranchId, @Date, @StartMin, @EndMin, @BoxId, @ServiceId, @ServiceName, @Price, @VehicleType, @Vehicle, @Plate,
                    @ClientName, @Phone, @Notes, @Status, @Origin, @RescheduledFrom, @CreatedAt, @UpdatedAt, @Active)
            """;
        await conn.ExecuteAsync(Cmd(sql, b));
        if (_lockedDate == b.Date.Date) _lockedBookings?.Add(b);
    }

    public Task UpdateBookingStatusAsync(Guid id, string status, DateTime updatedAtUtc) =>
        conn.ExecuteAsync(Cmd(
            "UPDATE AgendaBookings SET Status = @Status, UpdatedAt = @Utc WHERE Id = @Id AND BranchId = @BranchId",
            new { Id = id, BranchId = branchId, Status = status, Utc = updatedAtUtc }));

    public Task<AgendaClient?> GetClientAsync(IReadOnlyList<string> phoneVariants) =>
        conn.QueryFirstOrDefaultAsync<AgendaClient?>(Cmd(
            $"SELECT TOP (1) {AgendaRepository.ClientColumns} FROM AgendaClients WITH (UPDLOCK, HOLDLOCK) WHERE BranchId = @BranchId AND Active = 1 AND Phone IN @Phones ORDER BY UpdatedAt DESC",
            new { BranchId = branchId, Phones = phoneVariants }));

    public Task UpsertClientAsync(AgendaClient c)
    {
        const string sql = """
            UPDATE AgendaClients SET Name = @Name, ReminderOptIn = @ReminderOptIn, OptInAt = @OptInAt, UpdatedAt = @UpdatedAt, Active = 1
             WHERE Id = @Id;
            IF @@ROWCOUNT = 0
                INSERT INTO AgendaClients (Id, BranchId, Phone, Name, ReminderOptIn, OptInAt, CreatedAt, UpdatedAt, Active)
                VALUES (@Id, @BranchId, @Phone, @Name, @ReminderOptIn, @OptInAt, @CreatedAt, @UpdatedAt, 1);
            """;
        return conn.ExecuteAsync(Cmd(sql, c));
    }

    public Task InsertReminderAsync(AgendaReminder r)
    {
        const string sql = """
            INSERT INTO AgendaReminders (Id, BranchId, BookingId, Type, ScheduledFor, Status, Attempts, WamId, Answer, AnsweredAt, Error, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @BranchId, @BookingId, @Type, @ScheduledFor, @Status, @Attempts, @WamId, @Answer, @AnsweredAt, @Error, @CreatedAt, @UpdatedAt, @Active)
            """;
        return conn.ExecuteAsync(Cmd(sql, r));
    }

    public async Task<IReadOnlyList<AgendaReminder>> RemindersOfBookingAsync(Guid bookingId) =>
        (await conn.QueryAsync<AgendaReminder>(Cmd(
            $"SELECT {AgendaRepository.ReminderColumns} FROM AgendaReminders WITH (UPDLOCK) WHERE BookingId = @BookingId AND BranchId = @BranchId AND Active = 1",
            new { BookingId = bookingId, BranchId = branchId }))).AsList();

    public Task UpdateReminderAsync(AgendaReminder r)
    {
        const string sql = """
            UPDATE AgendaReminders
               SET Status = @Status, Attempts = @Attempts, WamId = @WamId, Answer = @Answer, AnsweredAt = @AnsweredAt, Error = @Error, UpdatedAt = @UpdatedAt
             WHERE Id = @Id
            """;
        return conn.ExecuteAsync(Cmd(sql, r));
    }

    public async Task<IReadOnlyList<AgendaReminder>> PendingRemindersAsync(DateTime until, IReadOnlyList<Guid>? onlyIds)
    {
        // READPAST: duas execuções do worker ao mesmo tempo não pegam o mesmo lembrete.
        var filter = onlyIds is null ? "AND ScheduledFor <= @Until" : "AND Id IN @Ids";
        var sql = $"""
            SELECT {AgendaRepository.ReminderColumns}
            FROM AgendaReminders WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE BranchId = @BranchId AND Active = 1 AND Status = 'pending' {filter}
            ORDER BY ScheduledFor
            """;
        return (await conn.QueryAsync<AgendaReminder>(Cmd(sql, new { BranchId = branchId, Until = until, Ids = onlyIds ?? [] }))).AsList();
    }

    public Task<AgendaReminder?> ReminderByWamIdAsync(string wamId) =>
        conn.QueryFirstOrDefaultAsync<AgendaReminder?>(Cmd(
            $"SELECT TOP (1) {AgendaRepository.ReminderColumns} FROM AgendaReminders WITH (UPDLOCK) WHERE BranchId = @BranchId AND WamId = @WamId AND Active = 1",
            new { BranchId = branchId, WamId = wamId }));

    public Task CancelPendingRemindersOfPhonesAsync(IReadOnlyList<string> phoneVariants) =>
        conn.ExecuteAsync(Cmd("""
            UPDATE r SET r.Status = 'canceled', r.UpdatedAt = @Utc
            FROM AgendaReminders r
            INNER JOIN AgendaBookings b ON b.Id = r.BookingId
            WHERE r.BranchId = @BranchId AND r.Status = 'pending' AND r.Active = 1 AND b.Phone IN @Phones
            """, new { BranchId = branchId, Phones = phoneVariants, Utc = DateTime.UtcNow }));
}
