using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly SqlSession _session;

    public UserRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, company_id AS CompanyId, email AS Email, password_hash AS PasswordHash,
        full_name AS FullName, display_name AS DisplayName, role AS Role, job_title AS JobTitle,
        phone AS Phone, avatar_url AS AvatarUrl, is_active AS IsActive, email_verified AS EmailVerified,
        last_login_at AS LastLoginAt, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<User>> GetAllByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Users WHERE company_id = @CompanyId ORDER BY is_active DESC, full_name";
        var list = await _session.Connection
            .QueryAsync<User>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Users WHERE id = @Id";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<User>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Users WHERE email = @Email";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<User>(new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM ABASTA_Users WHERE email = @Email AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;
        var exists = await _session.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Email = email, ExcludeId = excludeUserId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return exists == 1;
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_Users
                (id, company_id, email, password_hash, full_name, display_name, role, job_title,
                 phone, avatar_url, is_active, email_verified, last_login_at, created_at, updated_at)
            VALUES
                (@Id, @CompanyId, @Email, @PasswordHash, @FullName, @DisplayName, @Role, @JobTitle,
                 @Phone, @AvatarUrl, @IsActive, @EmailVerified, @LastLoginAt, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return user;
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_Users
            SET full_name = @FullName, display_name = @DisplayName, role = @Role, job_title = @JobTitle,
                phone = @Phone, avatar_url = @AvatarUrl, is_active = @IsActive, email_verified = @EmailVerified,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_Users SET is_active = 0, updated_at = @Now WHERE id = @Id AND company_id = @CompanyId AND is_active = 1";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, CompanyId = companyId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task UpdateLastLoginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_Users SET last_login_at = @Now WHERE id = @Id";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task MarkEmailVerifiedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_Users SET email_verified = 1, updated_at = @Now WHERE id = @Id";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
