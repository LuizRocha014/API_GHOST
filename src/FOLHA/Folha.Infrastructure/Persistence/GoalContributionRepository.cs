using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class GoalContributionRepository : IGoalContributionRepository
{
    private readonly SqlSession _session;

    public GoalContributionRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, goal_id AS GoalId, transaction_id AS TransactionId,
        amount AS Amount, contributed_at AS ContributedAt, note AS Note,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<GoalContribution>> GetAllByGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_GoalContributions WHERE goal_id = @GoalId ORDER BY contributed_at DESC";
        var list = await _session.Connection
            .QueryAsync<GoalContribution>(new CommandDefinition(sql, new { GoalId = goalId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<GoalContribution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_GoalContributions WHERE id = @Id";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<GoalContribution>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<GoalContribution> AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_GoalContributions
                (id, goal_id, transaction_id, amount, contributed_at, note, created_at, updated_at)
            VALUES
                (@Id, @GoalId, @TransactionId, @Amount, @ContributedAt, @Note, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, contribution, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return contribution;
    }

    public async Task<bool> UpdateAsync(GoalContribution contribution, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_GoalContributions
            SET transaction_id = @TransactionId, amount = @Amount,
                contributed_at = @ContributedAt, note = @Note, updated_at = @UpdatedAt
            WHERE id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, contribution, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_GoalContributions WHERE id = @Id";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
