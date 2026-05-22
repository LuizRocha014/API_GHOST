using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class RecurrenceRepository : IRecurrenceRepository
{
    private readonly SqlSession _session;

    public RecurrenceRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, frequency AS Frequency, every_n AS EveryN,
        day_of_month AS DayOfMonth, day_of_week AS DayOfWeek, month_of_year AS MonthOfYear,
        start_date AS StartDate, end_date AS EndDate, max_occurrences AS MaxOccurrences,
        last_generated_at AS LastGeneratedAt, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Recurrence>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Recurrences WHERE user_id = @UserId ORDER BY created_at DESC";
        var list = await _session.Connection
            .QueryAsync<Recurrence>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Recurrence?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Recurrences WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Recurrence>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Recurrence> AddAsync(Recurrence recurrence, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Recurrences
                (id, user_id, frequency, every_n, day_of_month, day_of_week, month_of_year,
                 start_date, end_date, max_occurrences, last_generated_at, created_at, updated_at)
            VALUES
                (@Id, @UserId, @Frequency, @EveryN, @DayOfMonth, @DayOfWeek, @MonthOfYear,
                 @StartDate, @EndDate, @MaxOccurrences, @LastGeneratedAt, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, recurrence, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return recurrence;
    }

    public async Task<bool> UpdateAsync(Recurrence recurrence, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Recurrences
            SET frequency = @Frequency, every_n = @EveryN, day_of_month = @DayOfMonth,
                day_of_week = @DayOfWeek, month_of_year = @MonthOfYear,
                start_date = @StartDate, end_date = @EndDate,
                max_occurrences = @MaxOccurrences, last_generated_at = @LastGeneratedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, recurrence, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_Recurrences WHERE id = @Id AND user_id = @UserId";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
