using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class BranchRepository : IBranchRepository
{
    private readonly SqlSession _session;

    public BranchRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, CreatedAt, UpdatedAt, Active
            FROM Branches
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Branch>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Branch>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, CreatedAt, UpdatedAt, Active
            FROM Branches
            WHERE CompanyId = @CompanyId AND Active = 1
            ORDER BY Name
            """;
        var list = await _session.Connection
            .QueryAsync<Branch>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<IReadOnlyList<Branch>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CompanyId, Name, CreatedAt, UpdatedAt, Active
            FROM Branches
            WHERE Active = 1
            ORDER BY Name
            """;
        var list = await _session.Connection
            .QueryAsync<Branch>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Branch> AddAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Branches (Id, CompanyId, Name, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @CompanyId, @Name, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, branch, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return branch;
    }

    public async Task<bool> UpdateAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Branches
            SET Name = @Name, UpdatedAt = @UpdatedAt, Active = @Active
            WHERE Id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, branch, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sel = "SELECT Active FROM Branches WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(sel, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string sql = """
            UPDATE Branches
            SET Active = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }
}
