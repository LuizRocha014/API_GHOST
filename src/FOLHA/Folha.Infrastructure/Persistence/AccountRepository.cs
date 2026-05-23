using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class AccountRepository : IAccountRepository
{
    private readonly SqlSession _session;

    public AccountRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, name AS Name, kind AS Kind, institution AS Institution,
        icon AS Icon, color_hex AS ColorHex, initial_balance AS InitialBalance,
        currency_code AS CurrencyCode, is_archived AS IsArchived,
        include_in_total AS IncludeInTotal, sort_order AS SortOrder,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Account>> GetAllByUserAsync(Guid userId, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columns} FROM FOLHA_Accounts
            WHERE user_id = @UserId
              AND (@ModifiedSince IS NULL OR updated_at > @ModifiedSince)
            ORDER BY sort_order, name
            """;
        var list = await _session.Connection
            .QueryAsync<Account>(new CommandDefinition(sql, new { UserId = userId, ModifiedSince = modifiedSince }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Accounts WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Account>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Account> AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Accounts
                (id, user_id, name, kind, institution, icon, color_hex, initial_balance,
                 currency_code, is_archived, include_in_total, sort_order, created_at, updated_at)
            VALUES
                (@Id, @UserId, @Name, @Kind, @Institution, @Icon, @ColorHex, @InitialBalance,
                 @CurrencyCode, @IsArchived, @IncludeInTotal, @SortOrder, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, account, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return account;
    }

    public async Task<bool> UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Accounts
            SET name = @Name, kind = @Kind, institution = @Institution, icon = @Icon,
                color_hex = @ColorHex, initial_balance = @InitialBalance,
                currency_code = @CurrencyCode, is_archived = @IsArchived,
                include_in_total = @IncludeInTotal, sort_order = @SortOrder,
                updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, account, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Accounts SET is_archived = 1, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND is_archived = 0
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
