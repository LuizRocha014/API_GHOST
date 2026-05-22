using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly SqlSession _session;

    public UserRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active
            FROM Users
            WHERE Active = 1
            ORDER BY Name
            """;
        var list = await _session.Connection
            .QueryAsync<User>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active
            FROM Users
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<User>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        const string sql = """
            SELECT Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active
            FROM Users
            WHERE Email = @Email AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<User>(new CommandDefinition(sql, new { Email = normalized }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        const string sql = """
            SELECT Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active
            FROM Users
            WHERE Username = @Username AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<User>(new CommandDefinition(sql, new { Username = normalized }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Users
                WHERE Email = @Email AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;
        var exists = await _session.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Email = normalized, ExcludeId = excludeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return exists == 1;
    }

    public async Task<bool> UsernameExistsAsync(string username, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Users
                WHERE Username = @Username AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;
        var exists = await _session.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Username = normalized, ExcludeId = excludeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return exists == 1;
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Users (Id, Name, Username, Email, PasswordHash, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @Name, @Username, @Email, @PasswordHash, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return user;
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Users
            SET Name = @Name, Username = @Username, Email = @Email, PasswordHash = @PasswordHash,
                UpdatedAt = @UpdatedAt, Active = @Active
            WHERE Id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sel = "SELECT Active FROM Users WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(sel, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string sql = """
            UPDATE Users
            SET Active = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }
}
