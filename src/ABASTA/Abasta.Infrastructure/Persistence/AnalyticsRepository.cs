using Abasta.Application.Abstractions;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class AnalyticsRepository : IAnalyticsRepository
{
    private readonly SqlSession _session;

    public AnalyticsRepository(SqlSession session) => _session = session;

    public async Task<IReadOnlyList<AnalyticsUser>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, full_name AS FullName, job_title AS JobTitle, email AS Email, role AS Role
            FROM ABASTA_Users
            WHERE company_id = @CompanyId AND is_active = 1
            ORDER BY full_name
            """;
        var rows = await _session.Connection
            .QueryAsync<AnalyticsUser>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AnalyticsEntry>> GetEntriesAsync(Guid companyId, Guid? userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT fe.id AS Id, fe.user_id AS UserId, ft.code AS FuelCode,
                   fe.station_name AS StationName, fe.liters AS Liters,
                   fe.price_per_liter AS PricePerLiter, fe.total_amount AS TotalAmount,
                   fe.odometer_km AS OdometerKm, fe.fueled_at AS FueledAt
            FROM ABASTA_FuelEntries fe
            JOIN ABASTA_FuelTypes ft ON ft.id = fe.fuel_type_id
            WHERE fe.company_id = @CompanyId AND fe.deleted_at IS NULL
              AND (@UserId IS NULL OR fe.user_id = @UserId)
            ORDER BY fe.fueled_at DESC
            """;
        var rows = await _session.Connection
            .QueryAsync<AnalyticsEntry>(new CommandDefinition(sql,
                new { CompanyId = companyId, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserVehiclePlatesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT va.user_id AS UserId, v.plate AS Plate
            FROM ABASTA_VehicleAssignments va
            JOIN ABASTA_Vehicles v ON v.id = va.vehicle_id
            WHERE va.company_id = @CompanyId AND va.unassigned_at IS NULL
            """;
        var rows = await _session.Connection
            .QueryAsync<(Guid UserId, string Plate)>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        var map = new Dictionary<Guid, string>();
        foreach (var r in rows) map[r.UserId] = r.Plate;
        return map;
    }

    public async Task<string?> GetAccountNameAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TOP 1 distributor FROM ABASTA_FuelAccounts WHERE company_id = @CompanyId AND is_active = 1 ORDER BY created_at";
        return await _session.Connection
            .ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<decimal?> GetBudgetAsync(Guid companyId, Guid? userId, DateTime month, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP 1 amount_limit FROM ABASTA_Budgets
            WHERE company_id = @CompanyId AND reference_month = @Month
              AND ((@UserId IS NOT NULL AND user_id = @UserId)
                   OR (user_id IS NULL AND vehicle_id IS NULL))
            ORDER BY CASE WHEN user_id IS NOT NULL THEN 0 ELSE 1 END
            """;
        return await _session.Connection
            .ExecuteScalarAsync<decimal?>(new CommandDefinition(sql,
                new { CompanyId = companyId, UserId = userId, Month = month.Date }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
