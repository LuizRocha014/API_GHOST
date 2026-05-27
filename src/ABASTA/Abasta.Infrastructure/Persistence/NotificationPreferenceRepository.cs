using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Dapper;

namespace Abasta.Infrastructure.Persistence;

public sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly SqlSession _session;

    public NotificationPreferenceRepository(SqlSession session) => _session = session;

    public async Task<NotificationPreference?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT user_id AS UserId, new_receipt_enabled AS NewReceiptEnabled,
                   weekly_report_enabled AS WeeklyReportEnabled, over_budget_enabled AS OverBudgetEnabled,
                   updated_at AS UpdatedAt
            FROM ABASTA_NotificationPreferences WHERE user_id = @UserId
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<NotificationPreference>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpsertAsync(NotificationPreference p, CancellationToken cancellationToken = default)
    {
        const string sql = """
            MERGE dbo.ABASTA_NotificationPreferences AS target
            USING (SELECT @UserId AS user_id) AS source
            ON target.user_id = source.user_id
            WHEN MATCHED THEN
                UPDATE SET new_receipt_enabled = @NewReceiptEnabled,
                           weekly_report_enabled = @WeeklyReportEnabled,
                           over_budget_enabled = @OverBudgetEnabled,
                           updated_at = @UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (user_id, new_receipt_enabled, weekly_report_enabled, over_budget_enabled, updated_at)
                VALUES (@UserId, @NewReceiptEnabled, @WeeklyReportEnabled, @OverBudgetEnabled, @UpdatedAt);
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
