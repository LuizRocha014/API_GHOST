using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class TransferRepository : ITransferRepository
{
    private readonly SqlSession _session;

    public TransferRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, from_account_id AS FromAccountId,
        to_account_id AS ToAccountId, amount AS Amount, fee AS Fee,
        occurred_at AS OccurredAt, description AS Description, notes AS Notes,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Transfer>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Transfers WHERE user_id = @UserId ORDER BY occurred_at DESC";
        var list = await _session.Connection
            .QueryAsync<Transfer>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Transfer?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Transfers WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Transfer>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Transfer> AddAsync(Transfer transfer, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Transfers
                (id, user_id, from_account_id, to_account_id, amount, fee,
                 occurred_at, description, notes, created_at, updated_at)
            VALUES
                (@Id, @UserId, @FromAccountId, @ToAccountId, @Amount, @Fee,
                 @OccurredAt, @Description, @Notes, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, transfer, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return transfer;
    }

    public async Task<bool> UpdateAsync(Transfer transfer, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Transfers
            SET from_account_id = @FromAccountId, to_account_id = @ToAccountId,
                amount = @Amount, fee = @Fee, occurred_at = @OccurredAt,
                description = @Description, notes = @Notes, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, transfer, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_Transfers WHERE id = @Id AND user_id = @UserId";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
