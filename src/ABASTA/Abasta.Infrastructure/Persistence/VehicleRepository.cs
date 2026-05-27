using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly SqlSession _session;

    public VehicleRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, company_id AS CompanyId, fuel_account_id AS FuelAccountId,
        default_fuel_type_id AS DefaultFuelTypeId, plate AS Plate, label AS Label,
        make AS Make, model AS Model, model_year AS ModelYear,
        tank_capacity_liters AS TankCapacityLiters, current_odometer_km AS CurrentOdometerKm,
        monthly_budget AS MonthlyBudget, is_active AS IsActive,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Vehicle>> GetAllByCompanyAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columns} FROM ABASTA_Vehicles
            WHERE company_id = @CompanyId
            ORDER BY is_active DESC, plate
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """;
        var list = await _session.Connection
            .QueryAsync<Vehicle>(new CommandDefinition(sql, new { CompanyId = companyId, Skip = skip, Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Vehicles WHERE id = @Id AND company_id = @CompanyId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Vehicle>(new CommandDefinition(sql, new { Id = id, CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Vehicle> AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_Vehicles
                (id, company_id, fuel_account_id, default_fuel_type_id, plate, label,
                 make, model, model_year, tank_capacity_liters, current_odometer_km,
                 monthly_budget, is_active, created_at, updated_at)
            VALUES
                (@Id, @CompanyId, @FuelAccountId, @DefaultFuelTypeId, @Plate, @Label,
                 @Make, @Model, @ModelYear, @TankCapacityLiters, @CurrentOdometerKm,
                 @MonthlyBudget, @IsActive, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, vehicle, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return vehicle;
    }

    public async Task<bool> UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_Vehicles
            SET fuel_account_id = @FuelAccountId, default_fuel_type_id = @DefaultFuelTypeId,
                plate = @Plate, label = @Label, make = @Make, model = @Model,
                model_year = @ModelYear, tank_capacity_liters = @TankCapacityLiters,
                current_odometer_km = @CurrentOdometerKm, monthly_budget = @MonthlyBudget,
                is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id AND company_id = @CompanyId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, vehicle, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_Vehicles SET is_active = 0, updated_at = @Now WHERE id = @Id AND company_id = @CompanyId AND is_active = 1";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, CompanyId = companyId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
