using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class GoalRepository : IGoalRepository
{
    private readonly SqlSession _session;

    public GoalRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, account_id AS AccountId, title AS Title,
        description AS Description, target_amount AS TargetAmount,
        current_amount AS CurrentAmount, target_date AS TargetDate,
        icon AS Icon, color_hex AS ColorHex, is_completed AS IsCompleted,
        completed_at AS CompletedAt, is_archived AS IsArchived,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Goal>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Goals WHERE user_id = @UserId ORDER BY is_archived, created_at DESC";
        var list = await _session.Connection
            .QueryAsync<Goal>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Goals WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Goal>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Goal> AddAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Goals
                (id, user_id, account_id, title, description, target_amount, current_amount,
                 target_date, icon, color_hex, is_completed, completed_at, is_archived,
                 created_at, updated_at)
            VALUES
                (@Id, @UserId, @AccountId, @Title, @Description, @TargetAmount, @CurrentAmount,
                 @TargetDate, @Icon, @ColorHex, @IsCompleted, @CompletedAt, @IsArchived,
                 @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, goal, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return goal;
    }

    public async Task<bool> UpdateAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Goals
            SET account_id = @AccountId, title = @Title, description = @Description,
                target_amount = @TargetAmount, current_amount = @CurrentAmount,
                target_date = @TargetDate, icon = @Icon, color_hex = @ColorHex,
                is_completed = @IsCompleted, completed_at = @CompletedAt,
                is_archived = @IsArchived, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, goal, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Goals SET is_archived = 1, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND is_archived = 0
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
