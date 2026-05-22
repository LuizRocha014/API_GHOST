using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class SessionRepository : ISessionRepository
{
    private readonly SqlSession _session;

    public SessionRepository(SqlSession session) => _session = session;

    public async Task<Session> AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Sessions
                (id, user_id, token_hash, user_agent, ip_address, expires_at, revoked_at, created_at)
            VALUES
                (@Id, @UserId, @TokenHash, @UserAgent, @IpAddress, @ExpiresAt, @RevokedAt, @CreatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, session, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return session;
    }

    public async Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, user_id AS UserId, token_hash AS TokenHash,
                   user_agent AS UserAgent, ip_address AS IpAddress,
                   expires_at AS ExpiresAt, revoked_at AS RevokedAt, created_at AS CreatedAt
            FROM FOLHA_Sessions
            WHERE token_hash = @TokenHash
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Session>(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE FOLHA_Sessions SET revoked_at = @Now WHERE id = @Id AND revoked_at IS NULL";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
