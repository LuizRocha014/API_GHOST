using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class InvitationRepository : IInvitationRepository
{
    private readonly SqlSession _session;

    public InvitationRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, company_id AS CompanyId, email AS Email, role AS Role,
        invited_by_user_id AS InvitedByUserId, token_hash AS TokenHash, status AS Status,
        expires_at AS ExpiresAt, accepted_at AS AcceptedAt, accepted_user_id AS AcceptedUserId,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<Invitation> AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_Invitations
                (id, company_id, email, role, invited_by_user_id, token_hash, status,
                 expires_at, accepted_at, accepted_user_id, created_at, updated_at)
            VALUES
                (@Id, @CompanyId, @Email, @Role, @InvitedByUserId, @TokenHash, @Status,
                 @ExpiresAt, @AcceptedAt, @AcceptedUserId, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, invitation, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return invitation;
    }

    public async Task<Invitation?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Invitations WHERE token_hash = @TokenHash";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Invitation>(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Invitation>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM ABASTA_Invitations WHERE company_id = @CompanyId ORDER BY created_at DESC";
        var list = await _session.Connection
            .QueryAsync<Invitation>(new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<bool> UpdateStatusAsync(Guid id, string status, Guid? acceptedUserId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ABASTA_Invitations
            SET status = @Status,
                accepted_user_id = @AcceptedUserId,
                accepted_at = CASE WHEN @Status = 'accepted' THEN @Now ELSE accepted_at END,
                updated_at = @Now
            WHERE id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, Status = status, AcceptedUserId = acceptedUserId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
