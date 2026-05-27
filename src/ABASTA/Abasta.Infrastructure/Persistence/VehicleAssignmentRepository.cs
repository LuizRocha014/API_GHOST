using Abasta.Application.Abstractions;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class VehicleAssignmentRepository : IVehicleAssignmentRepository
{
    private readonly SqlSession _session;

    public VehicleAssignmentRepository(SqlSession session) => _session = session;

    public async Task AssignPrimaryAsync(Guid companyId, Guid vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_VehicleAssignments
            SET unassigned_at = @Now, updated_at = @Now
            WHERE company_id = @CompanyId AND unassigned_at IS NULL
              AND (vehicle_id = @VehicleId OR user_id = @UserId);

            INSERT INTO ABASTA_VehicleAssignments
                (id, company_id, vehicle_id, user_id, is_primary, assigned_at, created_at, updated_at)
            VALUES (NEWID(), @CompanyId, @VehicleId, @UserId, 1, @Now, @Now, @Now);
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql,
                new { CompanyId = companyId, VehicleId = vehicleId, UserId = userId, Now = DateTime.UtcNow },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Guid?> GetActiveVehicleIdAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP 1 vehicle_id
            FROM ABASTA_VehicleAssignments
            WHERE company_id = @CompanyId AND user_id = @UserId AND unassigned_at IS NULL
            ORDER BY is_primary DESC, assigned_at DESC
            """;
        return await _session.Connection
            .ExecuteScalarAsync<Guid?>(new CommandDefinition(sql,
                new { CompanyId = companyId, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
