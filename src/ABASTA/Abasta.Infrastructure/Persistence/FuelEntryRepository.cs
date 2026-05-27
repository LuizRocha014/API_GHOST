using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class FuelEntryRepository : IFuelEntryRepository
{
    private readonly SqlSession _session;

    public FuelEntryRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, company_id AS CompanyId, user_id AS UserId, vehicle_id AS VehicleId,
        fuel_type_id AS FuelTypeId, station_id AS StationId, receipt_id AS ReceiptId,
        station_name AS StationName, liters AS Liters, price_per_liter AS PricePerLiter,
        total_amount AS TotalAmount, odometer_km AS OdometerKm, fueled_at AS FueledAt,
        status AS Status, source AS Source, notes AS Notes, deleted_at AS DeletedAt,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<FuelEntry>> GetAllByCompanyAsync(
        Guid companyId, Guid? userId, Guid? vehicleId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columns} FROM ABASTA_FuelEntries
            WHERE company_id = @CompanyId AND deleted_at IS NULL
              AND (@UserId IS NULL OR user_id = @UserId)
              AND (@VehicleId IS NULL OR vehicle_id = @VehicleId)
            ORDER BY fueled_at DESC, created_at DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """;
        var list = await _session.Connection
            .QueryAsync<FuelEntry>(new CommandDefinition(sql,
                new { CompanyId = companyId, UserId = userId, VehicleId = vehicleId, Skip = skip, Take = take },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<FuelEntry?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_FuelEntries WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<FuelEntry>(new CommandDefinition(sql, new { Id = id, CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<FuelEntry> AddAsync(FuelEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_FuelEntries
                (id, company_id, user_id, vehicle_id, fuel_type_id, station_id, receipt_id,
                 station_name, liters, price_per_liter, total_amount, odometer_km, fueled_at,
                 status, source, notes, created_at, updated_at)
            VALUES
                (@Id, @CompanyId, @UserId, @VehicleId, @FuelTypeId, @StationId, @ReceiptId,
                 @StationName, @Liters, @PricePerLiter, @TotalAmount, @OdometerKm, @FueledAt,
                 @Status, @Source, @Notes, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, entry, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return entry;
    }

    public async Task<bool> UpdateAsync(FuelEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_FuelEntries
            SET fuel_type_id = @FuelTypeId, station_id = @StationId, station_name = @StationName,
                liters = @Liters, price_per_liter = @PricePerLiter, total_amount = @TotalAmount,
                odometer_km = @OdometerKm, fueled_at = @FueledAt, status = @Status,
                notes = @Notes, updated_at = @UpdatedAt
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, entry, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_FuelEntries SET deleted_at = @Now, updated_at = @Now
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, CompanyId = companyId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
