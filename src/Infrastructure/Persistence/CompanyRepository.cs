using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly SqlSession _session;

    public CompanyRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Cnpj, CreatedAt, UpdatedAt, Active
            FROM Companies
            WHERE Active = 1
            ORDER BY Name
            """;
        var list = await _session.Connection
            .QueryAsync<Company>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Cnpj, CreatedAt, UpdatedAt, Active
            FROM Companies
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Company>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Company> AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Companies (Id, Name, Cnpj, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @Name, @Cnpj, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, company, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return company;
    }

    public async Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Companies
            SET Name = @Name, Cnpj = @Cnpj, UpdatedAt = @UpdatedAt, Active = @Active
            WHERE Id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, company, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sel = "SELECT Active FROM Companies WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(sel, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string sql = """
            UPDATE Companies
            SET Active = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }
}
