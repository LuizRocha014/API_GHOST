using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class CreditCardRepository : ICreditCardRepository
{
    private readonly SqlSession _session;

    public CreditCardRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, account_id AS AccountId, name AS Name, brand AS Brand,
        last_four AS LastFour, credit_limit AS CreditLimit, closing_day AS ClosingDay,
        due_day AS DueDay, is_archived AS IsArchived,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<CreditCard>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCards WHERE user_id = @UserId ORDER BY name";
        var list = await _session.Connection
            .QueryAsync<CreditCard>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<CreditCard?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCards WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<CreditCard>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<CreditCard> AddAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_CreditCards
                (id, user_id, account_id, name, brand, last_four, credit_limit,
                 closing_day, due_day, is_archived, created_at, updated_at)
            VALUES
                (@Id, @UserId, @AccountId, @Name, @Brand, @LastFour, @CreditLimit,
                 @ClosingDay, @DueDay, @IsArchived, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, card, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return card;
    }

    public async Task<bool> UpdateAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_CreditCards
            SET account_id = @AccountId, name = @Name, brand = @Brand, last_four = @LastFour,
                credit_limit = @CreditLimit, closing_day = @ClosingDay, due_day = @DueDay,
                is_archived = @IsArchived, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, card, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_CreditCards SET is_archived = 1, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND is_archived = 0
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
