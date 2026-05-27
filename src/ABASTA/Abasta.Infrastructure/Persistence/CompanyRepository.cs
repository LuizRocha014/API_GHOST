using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly SqlSession _session;

    public CompanyRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, name AS Name, legal_name AS LegalName, cnpj AS Cnpj,
        monthly_budget AS MonthlyBudget, timezone AS Timezone, currency_code AS CurrencyCode,
        is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Companies WHERE id = @Id";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Company>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Company> AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_Companies
                (id, name, legal_name, cnpj, monthly_budget, timezone, currency_code, is_active, created_at, updated_at)
            VALUES
                (@Id, @Name, @LegalName, @Cnpj, @MonthlyBudget, @Timezone, @CurrencyCode, @IsActive, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, company, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return company;
    }

    public async Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_Companies
            SET name = @Name, legal_name = @LegalName, cnpj = @Cnpj, monthly_budget = @MonthlyBudget,
                timezone = @Timezone, currency_code = @CurrencyCode, is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, company, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
