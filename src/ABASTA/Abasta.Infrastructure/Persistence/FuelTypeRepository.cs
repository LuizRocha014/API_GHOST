using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class FuelTypeRepository : IFuelTypeRepository
{
    private readonly SqlSession _session;

    public FuelTypeRepository(SqlSession session) => _session = session;

    public async Task<IReadOnlyList<FuelType>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, code AS Code, label AS Label, color_hex AS ColorHex,
                   sort_order AS SortOrder, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM ABASTA_FuelTypes
            WHERE is_active = 1
            ORDER BY sort_order, label
            """;
        var list = await _session.Connection
            .QueryAsync<FuelType>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }
}
