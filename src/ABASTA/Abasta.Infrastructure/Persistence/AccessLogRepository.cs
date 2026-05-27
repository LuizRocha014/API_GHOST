using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class AccessLogRepository : IAccessLogRepository
{
    private readonly SqlSession _session;

    public AccessLogRepository(SqlSession session) => _session = session;

    public async Task AddAsync(AccessLog log, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ABASTA_AccessLogs
                (id, company_id, user_id, email, event, succeeded, ip_address, user_agent, detail, created_at)
            VALUES
                (@Id, @CompanyId, @UserId, @Email, @Event, @Succeeded, @IpAddress, @UserAgent, @Detail, @CreatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, log, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AccessLog>> GetByCompanyAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, company_id AS CompanyId, user_id AS UserId, email AS Email,
                   event AS Event, succeeded AS Succeeded, ip_address AS IpAddress,
                   user_agent AS UserAgent, detail AS Detail, created_at AS CreatedAt
            FROM ABASTA_AccessLogs
            WHERE company_id = @CompanyId
            ORDER BY created_at DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """;
        var list = await _session.Connection
            .QueryAsync<AccessLog>(new CommandDefinition(sql, new { CompanyId = companyId, Skip = skip, Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }
}
