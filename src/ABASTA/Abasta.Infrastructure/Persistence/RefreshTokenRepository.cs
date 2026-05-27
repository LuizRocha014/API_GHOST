using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly SqlSession _session;

    public RefreshTokenRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, token_hash AS TokenHash, device_id AS DeviceId,
        user_agent AS UserAgent, ip_address AS IpAddress, expires_at AS ExpiresAt,
        revoked_at AS RevokedAt, replaced_by_token_id AS ReplacedByTokenId, created_at AS CreatedAt
    """;

    public async Task<RefreshToken> AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_RefreshTokens
                (id, user_id, token_hash, device_id, user_agent, ip_address,
                 expires_at, revoked_at, replaced_by_token_id, created_at)
            VALUES
                (@Id, @UserId, @TokenHash, @DeviceId, @UserAgent, @IpAddress,
                 @ExpiresAt, @RevokedAt, @ReplacedByTokenId, @CreatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, token, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return token;
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_RefreshTokens WHERE token_hash = @TokenHash";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<RefreshToken>(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> RevokeAsync(Guid id, Guid? replacedByTokenId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_RefreshTokens
            SET revoked_at = @Now, replaced_by_token_id = @ReplacedBy
            WHERE id = @Id AND revoked_at IS NULL
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Now = DateTime.UtcNow, ReplacedBy = replacedByTokenId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE ABASTA_RefreshTokens SET revoked_at = @Now WHERE user_id = @UserId AND revoked_at IS NULL";
        return await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
