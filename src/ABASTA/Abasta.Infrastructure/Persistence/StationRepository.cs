using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class StationRepository : IStationRepository
{
    private readonly SqlSession _session;

    public StationRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, company_id AS CompanyId, name AS Name, brand AS Brand, cnpj AS Cnpj,
        city AS City, state AS State, latitude AS Latitude, longitude AS Longitude,
        is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Station>> GetAllByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Stations WHERE company_id = @CompanyId ORDER BY is_active DESC, name";
        var list = await _session.Connection
            .QueryAsync<Station>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Station?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Stations WHERE id = @Id AND company_id = @CompanyId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Station>(new CommandDefinition(sql, new { Id = id, CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Station> AddAsync(Station station, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_Stations
                (id, company_id, name, brand, cnpj, city, state, latitude, longitude, is_active, created_at, updated_at)
            VALUES
                (@Id, @CompanyId, @Name, @Brand, @Cnpj, @City, @State, @Latitude, @Longitude, @IsActive, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, station, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return station;
    }

    public async Task<bool> UpdateAsync(Station station, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_Stations
            SET name = @Name, brand = @Brand, cnpj = @Cnpj, city = @City, state = @State,
                latitude = @Latitude, longitude = @Longitude, is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id AND company_id = @CompanyId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, station, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_Stations SET is_active = 0, updated_at = @Now WHERE id = @Id AND company_id = @CompanyId AND is_active = 1";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, CompanyId = companyId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
