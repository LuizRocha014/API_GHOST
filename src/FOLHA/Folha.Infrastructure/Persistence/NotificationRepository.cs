using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly SqlSession _session;

    public NotificationRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, kind AS Kind, title AS Title, body AS Body,
        related_kind AS RelatedKind, related_id AS RelatedId,
        scheduled_for AS ScheduledFor, is_read AS IsRead, read_at AS ReadAt,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Notification>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Notifications WHERE user_id = @UserId ORDER BY created_at DESC";
        var list = await _session.Connection
            .QueryAsync<Notification>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Notification?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Notifications WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Notification>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Notifications
                (id, user_id, kind, title, body, related_kind, related_id, scheduled_for,
                 is_read, read_at, created_at, updated_at)
            VALUES
                (@Id, @UserId, @Kind, @Title, @Body, @RelatedKind, @RelatedId, @ScheduledFor,
                 @IsRead, @ReadAt, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, notification, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return notification;
    }

    public async Task<bool> UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Notifications
            SET kind = @Kind, title = @Title, body = @Body, related_kind = @RelatedKind,
                related_id = @RelatedId, scheduled_for = @ScheduledFor,
                is_read = @IsRead, read_at = @ReadAt, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, notification, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Notifications
            SET is_read = 1, read_at = @Now, updated_at = @Now
            WHERE id = @Id AND user_id = @UserId AND is_read = 0
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_Notifications WHERE id = @Id AND user_id = @UserId";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
