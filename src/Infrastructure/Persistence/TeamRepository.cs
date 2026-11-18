using Application.Abstractions;
using Application.Agenda;
using Dapper;
using Domain.Entities;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence;

public sealed class TeamRepository(SqlSession session) : ITeamRepository
{
    private SqlConnection Conn => session.Connection;

    public async Task<AgendaUserAccess?> GetAccessAsync(Guid userId, Guid? branchId, CancellationToken ct = default)
    {
        // Sem vínculo nenhum com filiais, o usuário abre todas (ex.: o dono do seed).
        const string sql = """
            SELECT u.Active, u.Role, u.MustChangePassword,
                CAST(CASE
                    WHEN @BranchId IS NULL THEN 1
                    WHEN NOT EXISTS (SELECT 1 FROM Branches WHERE Id = @BranchId AND Active = 1) THEN 0
                    WHEN NOT EXISTS (SELECT 1 FROM UserCompanyBranches WHERE UserId = u.Id AND Active = 1) THEN 1
                    WHEN EXISTS (SELECT 1 FROM UserCompanyBranches WHERE UserId = u.Id AND BranchId = @BranchId AND Active = 1) THEN 1
                    ELSE 0
                END AS bit) AS BranchAllowed
            FROM Users u
            WHERE u.Id = @UserId
            """;
        var row = await Conn.QuerySingleOrDefaultAsync<AccessRow>(new CommandDefinition(sql, new { UserId = userId, BranchId = branchId }, cancellationToken: ct));
        return row is null ? null : new AgendaUserAccess(row.Active, row.Role ?? UserRoles.Owner, row.MustChangePassword, row.BranchAllowed);
    }

    private sealed class AccessRow
    {
        public bool Active { get; set; }
        public string? Role { get; set; }
        public bool MustChangePassword { get; set; }
        public bool BranchAllowed { get; set; }
    }

    public async Task<IReadOnlyList<TeamMemberRow>> ListMembersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Name, Username, Role, Active, MustChangePassword, LockedUntil, LastLoginAt
            FROM Users
            ORDER BY Active DESC, Name
            """;
        return (await Conn.QueryAsync<TeamMemberRow>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<(Guid UserId, Guid BranchId)>> ListMemberBranchesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT ucb.UserId, ucb.BranchId
            FROM UserCompanyBranches ucb
            INNER JOIN Branches b ON b.Id = ucb.BranchId AND b.Active = 1
            WHERE ucb.Active = 1
            """;
        return (await Conn.QueryAsync<(Guid, Guid)>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public Task<User?> GetUserAsync(Guid id, CancellationToken ct = default) =>
        Conn.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
            """
            SELECT Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active, Role, MustChangePassword, FailedLogins,
                   LockedUntil, LastLoginAt
            FROM Users WHERE Id = @Id
            """,
            new { Id = id }, cancellationToken: ct));

    public Task<int> CountActiveOwnersAsync(Guid? exceptUserId, CancellationToken ct = default) =>
        Conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Users WHERE Active = 1 AND Role = @Role AND (@Except IS NULL OR Id <> @Except)",
            new { Role = UserRoles.Owner, Except = exceptUserId }, cancellationToken: ct));

    public async Task SaveMemberAsync(User user, IReadOnlyList<Guid> branchIds, bool isNew, CancellationToken ct = default)
    {
        await using var tx = (SqlTransaction)await Conn.BeginTransactionAsync(ct);
        if (isNew)
        {
            await Conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO Users (Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active, Role, MustChangePassword,
                                   FailedLogins, LockedUntil, LastLoginAt)
                VALUES (@Id, @Name, @Username, @Email, @PasswordHash, @CreatedAt, @UpdatedAt, @Active, @Role, @MustChangePassword,
                        @FailedLogins, @LockedUntil, @LastLoginAt)
                """,
                user, tx, cancellationToken: ct));
        }
        else
        {
            await Conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE Users
                SET Name = @Name, Role = @Role, Active = @Active, PasswordHash = @PasswordHash, MustChangePassword = @MustChangePassword,
                    FailedLogins = @FailedLogins, LockedUntil = @LockedUntil, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                """,
                user, tx, cancellationToken: ct));
        }

        // Troca as filiais: desliga as que saíram e liga (ou cria) as escolhidas. Nenhuma = todas.
        var utc = DateTime.UtcNow;
        await Conn.ExecuteAsync(new CommandDefinition(
            "UPDATE UserCompanyBranches SET Active = 0, UpdatedAt = @Utc WHERE UserId = @UserId AND Active = 1 AND BranchId NOT IN @Keep",
            new { UserId = user.Id, Utc = utc, Keep = branchIds.Count == 0 ? new[] { Guid.Empty } : branchIds.ToArray() }, tx, cancellationToken: ct));
        foreach (var branchId in branchIds)
        {
            var updated = await Conn.ExecuteAsync(new CommandDefinition(
                "UPDATE UserCompanyBranches SET Active = 1, UpdatedAt = @Utc WHERE UserId = @UserId AND BranchId = @BranchId AND AccessId = @AccessId",
                new { UserId = user.Id, BranchId = branchId, AccessId = SeedIds.DefaultAccessId, Utc = utc }, tx, cancellationToken: ct));
            if (updated > 0) continue;
            await Conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO UserCompanyBranches (Id, UserId, CompanyId, BranchId, AccessId, CreatedAt, UpdatedAt, Active)
                SELECT @Id, @UserId, b.CompanyId, b.Id, @AccessId, @Utc, @Utc, 1 FROM Branches b WHERE b.Id = @BranchId
                """,
                new { Id = Guid.NewGuid(), UserId = user.Id, BranchId = branchId, AccessId = SeedIds.DefaultAccessId, Utc = utc },
                tx, cancellationToken: ct));
        }
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<CompanyBranchDto>> ListCompanyBranchesAsync(Guid companyId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT b.Id, b.Name, b.CompanyId, c.Name AS CompanyName
            FROM Branches b
            INNER JOIN Companies c ON c.Id = b.CompanyId
            WHERE b.CompanyId = @CompanyId AND b.Active = 1
            ORDER BY b.Name
            """;
        var rows = await Conn.QueryAsync<(Guid Id, string Name, Guid CompanyId, string CompanyName)>(
            new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: ct));
        return rows.Select(r => new CompanyBranchDto(r.Id.ToString(), r.Name, r.CompanyId.ToString(), r.CompanyName)).ToList();
    }

    public Task<Guid?> CompanyOfBranchAsync(Guid branchId, CancellationToken ct = default) =>
        Conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT CompanyId FROM Branches WHERE Id = @Id AND Active = 1", new { Id = branchId }, cancellationToken: ct));

    public async Task<Guid> CreateBranchAsync(Guid companyId, string name, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var utc = DateTime.UtcNow;
        await Conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Branches (Id, CompanyId, Name, CreatedAt, UpdatedAt, Active) VALUES (@Id, @CompanyId, @Name, @Utc, @Utc, 1)",
            new { Id = id, CompanyId = companyId, Name = name, Utc = utc }, cancellationToken: ct));
        return id;
    }

    public async Task<bool> RenameBranchAsync(Guid companyId, Guid branchId, string name, CancellationToken ct = default) =>
        await Conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Branches SET Name = @Name, UpdatedAt = @Utc WHERE Id = @Id AND CompanyId = @CompanyId AND Active = 1",
            new { Id = branchId, CompanyId = companyId, Name = name, Utc = DateTime.UtcNow }, cancellationToken: ct)) > 0;

    public async Task<bool> DeactivateBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default) =>
        await Conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Branches SET Active = 0, UpdatedAt = @Utc WHERE Id = @Id AND CompanyId = @CompanyId AND Active = 1",
            new { Id = branchId, CompanyId = companyId, Utc = DateTime.UtcNow }, cancellationToken: ct)) > 0;

    public Task RenameCompanyAsync(Guid companyId, string name, CancellationToken ct = default) =>
        Conn.ExecuteAsync(new CommandDefinition(
            "UPDATE Companies SET Name = @Name, UpdatedAt = @Utc WHERE Id = @Id",
            new { Id = companyId, Name = name, Utc = DateTime.UtcNow }, cancellationToken: ct));
}
