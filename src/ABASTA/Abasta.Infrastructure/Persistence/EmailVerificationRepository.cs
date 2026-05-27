using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class EmailVerificationRepository : IEmailVerificationRepository
{
    private readonly SqlSession _session;

    public EmailVerificationRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, email AS Email, code_hash AS CodeHash, purpose AS Purpose,
        attempts AS Attempts, expires_at AS ExpiresAt, consumed_at AS ConsumedAt, created_at AS CreatedAt
    """;

    public async Task AddAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_EmailVerifications
                (id, user_id, email, code_hash, purpose, attempts, expires_at, consumed_at, created_at)
            VALUES
                (@Id, @UserId, @Email, @CodeHash, @Purpose, @Attempts, @ExpiresAt, @ConsumedAt, @CreatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, verification, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<EmailVerification?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT TOP 1 {Columns} FROM ABASTA_EmailVerifications
            WHERE user_id = @UserId AND consumed_at IS NULL
            ORDER BY created_at DESC
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<EmailVerification>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task MarkConsumedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_EmailVerifications SET consumed_at = @Now WHERE id = @Id";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_EmailVerifications SET consumed_at = @Now WHERE user_id = @UserId AND consumed_at IS NULL";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task IncrementAttemptsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_EmailVerifications SET attempts = attempts + 1 WHERE id = @Id";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
