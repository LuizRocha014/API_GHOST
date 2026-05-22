using Application.Abstractions;
using Application.UserBranchAccesses;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class UserCompanyBranchRepository : IUserCompanyBranchRepository
{
    private readonly SqlSession _session;

    public UserCompanyBranchRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<UserBranchAccessDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ucb.Id,
                ucb.UserId,
                ucb.CompanyId,
                ucb.BranchId,
                ucb.AccessId,
                acc.Name AS AccessName,
                acc.Code AS AccessCode,
                b.Name AS BranchName,
                c.Name AS CompanyName,
                ucb.Active,
                ucb.CreatedAt,
                ucb.UpdatedAt
            FROM UserCompanyBranches ucb
            INNER JOIN Branches b ON b.Id = ucb.BranchId
            INNER JOIN Companies c ON c.Id = ucb.CompanyId
            INNER JOIN Accesses acc ON acc.Id = ucb.AccessId
            WHERE ucb.Id = @Id AND ucb.UserId = @UserId
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<UserBranchAccessDto>(
                new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserBranchAccessDto>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                ucb.Id,
                ucb.UserId,
                ucb.CompanyId,
                ucb.BranchId,
                ucb.AccessId,
                acc.Name AS AccessName,
                acc.Code AS AccessCode,
                b.Name AS BranchName,
                c.Name AS CompanyName,
                ucb.Active,
                ucb.CreatedAt,
                ucb.UpdatedAt
            FROM UserCompanyBranches ucb
            INNER JOIN Branches b ON b.Id = ucb.BranchId
            INNER JOIN Companies c ON c.Id = ucb.CompanyId
            INNER JOIN Accesses acc ON acc.Id = ucb.AccessId
            WHERE ucb.UserId = @UserId AND ucb.Active = 1
            ORDER BY c.Name, b.Name, acc.Name
            """;
        var list = await _session.Connection
            .QueryAsync<UserBranchAccessDto>(
                new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<UserCompanyBranch?> GetEntityByUserBranchAndAccessAsync(
        Guid userId,
        Guid branchId,
        Guid accessId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, UserId, CompanyId, BranchId, AccessId, CreatedAt, UpdatedAt, Active
            FROM UserCompanyBranches
            WHERE UserId = @UserId AND BranchId = @BranchId AND AccessId = @AccessId
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<UserCompanyBranch>(
                new CommandDefinition(sql, new { UserId = userId, BranchId = branchId, AccessId = accessId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task ReactivateAsync(Guid id, Guid userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE UserCompanyBranches
            SET Active = 1, CompanyId = @CompanyId, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND UserId = @UserId
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(
                    sql,
                    new { Id = id, UserId = userId, CompanyId = companyId, UpdatedAt = DateTime.UtcNow },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<UserCompanyBranch> AddAsync(UserCompanyBranch access, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO UserCompanyBranches (Id, UserId, CompanyId, BranchId, AccessId, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @UserId, @CompanyId, @BranchId, @AccessId, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, access, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return access;
    }

    public async Task UpdateActiveAsync(Guid id, Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE UserCompanyBranches
            SET Active = @Active, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND UserId = @UserId
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(
                    sql,
                    new { Id = id, UserId = userId, Active = active, UpdatedAt = DateTime.UtcNow },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sel = """
            SELECT Active FROM UserCompanyBranches
            WHERE Id = @Id AND UserId = @UserId
            """;
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(sel, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string sql = """
            UPDATE UserCompanyBranches
            SET Active = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND UserId = @UserId AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }
}
