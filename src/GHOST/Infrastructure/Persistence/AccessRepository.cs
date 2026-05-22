using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class AccessRepository : IAccessRepository
{
    private readonly SqlSession _session;

    public AccessRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<Access?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Code, CreatedAt, UpdatedAt, Active
            FROM Accesses
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Access>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Access>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Code, CreatedAt, UpdatedAt, Active
            FROM Accesses
            WHERE Active = 1
            ORDER BY Name
            """;
        var list = await _session.Connection
            .QueryAsync<Access>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Accesses
                WHERE Code IS NOT NULL AND LOWER(Code) = @Code
                  AND Active = 1
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;
        var n = await _session.Connection
            .ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sql,
                    new { Code = normalizedCode, ExcludeId = excludeId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n == 1;
    }

    public async Task<Access> AddAsync(Access access, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Accesses (Id, Name, Code, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @Name, @Code, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, access, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return access;
    }
}
