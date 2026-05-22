using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class BudgetRepository : IBudgetRepository
{
    private readonly SqlSession _session;

    public BudgetRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, category_id AS CategoryId,
        reference_month AS ReferenceMonth, amount_limit AS AmountLimit,
        rollover AS Rollover, alert_threshold AS AlertThreshold,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Budget>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Budgets WHERE user_id = @UserId ORDER BY reference_month DESC";
        var list = await _session.Connection
            .QueryAsync<Budget>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Budget?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Budgets WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Budget>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Budget> AddAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Budgets
                (id, user_id, category_id, reference_month, amount_limit, rollover,
                 alert_threshold, created_at, updated_at)
            VALUES
                (@Id, @UserId, @CategoryId, @ReferenceMonth, @AmountLimit, @Rollover,
                 @AlertThreshold, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, budget, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return budget;
    }

    public async Task<bool> UpdateAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Budgets
            SET category_id = @CategoryId, reference_month = @ReferenceMonth,
                amount_limit = @AmountLimit, rollover = @Rollover,
                alert_threshold = @AlertThreshold, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, budget, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_Budgets WHERE id = @Id AND user_id = @UserId";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
