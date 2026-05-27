using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class EmailVerificationRepository : IEmailVerificationRepository
{
    private readonly SqlSession _session;

    public EmailVerificationRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, code_hash AS CodeHash, expires_at AS ExpiresAt,
        consumed_at AS ConsumedAt, attempts AS Attempts, created_at AS CreatedAt
    """;

    public async Task AddAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_EmailVerifications
                (id, user_id, code_hash, expires_at, consumed_at, attempts, created_at)
            VALUES
                (@Id, @UserId, @CodeHash, @ExpiresAt, @ConsumedAt, @Attempts, @CreatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, verification, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<EmailVerification?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT TOP 1 {Columns}
            FROM FOLHA_EmailVerifications
            WHERE user_id = @UserId AND consumed_at IS NULL
            ORDER BY created_at DESC
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<EmailVerification>(
                new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task MarkConsumedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE FOLHA_EmailVerifications SET consumed_at = @Now WHERE id = @Id";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_EmailVerifications
            SET consumed_at = @Now
            WHERE user_id = @UserId AND consumed_at IS NULL
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task IncrementAttemptsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE FOLHA_EmailVerifications SET attempts = attempts + 1 WHERE id = @Id";
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
