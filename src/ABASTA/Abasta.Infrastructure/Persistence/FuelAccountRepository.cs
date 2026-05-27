using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class FuelAccountRepository : IFuelAccountRepository
{
    private readonly SqlSession _session;

    public FuelAccountRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, company_id AS CompanyId, distributor AS Distributor,
        account_number AS AccountNumber, is_active AS IsActive,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<FuelAccount>> GetAllByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_FuelAccounts WHERE company_id = @CompanyId ORDER BY is_active DESC, distributor";
        var list = await _session.Connection
            .QueryAsync<FuelAccount>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<FuelAccount?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_FuelAccounts WHERE id = @Id AND company_id = @CompanyId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<FuelAccount>(new CommandDefinition(sql, new { Id = id, CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<FuelAccount> AddAsync(FuelAccount account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_FuelAccounts (id, company_id, distributor, account_number, is_active, created_at, updated_at)
            VALUES (@Id, @CompanyId, @Distributor, @AccountNumber, @IsActive, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, account, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return account;
    }

    public async Task<bool> UpdateAsync(FuelAccount account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_FuelAccounts
            SET distributor = @Distributor, account_number = @AccountNumber, is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id AND company_id = @CompanyId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, account, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_FuelAccounts SET is_active = 0, updated_at = @Now WHERE id = @Id AND company_id = @CompanyId AND is_active = 1";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, CompanyId = companyId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
