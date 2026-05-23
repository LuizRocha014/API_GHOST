using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class BillRepository : IBillRepository
{
    private readonly SqlSession _session;

    public BillRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, account_id AS AccountId, category_id AS CategoryId,
        recurrence_id AS RecurrenceId, description AS Description, amount AS Amount,
        kind AS Kind, due_date AS DueDate, status AS Status,
        paid_amount AS PaidAmount, paid_at AS PaidAt, paid_transaction_id AS PaidTransactionId,
        installment_current AS InstallmentCurrent, installment_total AS InstallmentTotal,
        notes AS Notes, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Bill>> GetAllByUserAsync(Guid userId, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columns} FROM FOLHA_Bills
            WHERE user_id = @UserId
              AND (@ModifiedSince IS NULL OR updated_at > @ModifiedSince)
            ORDER BY due_date
            """;
        var list = await _session.Connection
            .QueryAsync<Bill>(new CommandDefinition(sql, new { UserId = userId, ModifiedSince = modifiedSince }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Bill?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Bills WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Bill>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Bill> AddAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Bills
                (id, user_id, account_id, category_id, recurrence_id, description,
                 amount, kind, due_date, status,
                 paid_amount, paid_at, paid_transaction_id,
                 installment_current, installment_total,
                 notes, created_at, updated_at)
            VALUES
                (@Id, @UserId, @AccountId, @CategoryId, @RecurrenceId, @Description,
                 @Amount, @Kind, @DueDate, @Status,
                 @PaidAmount, @PaidAt, @PaidTransactionId,
                 @InstallmentCurrent, @InstallmentTotal,
                 @Notes, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, bill, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return bill;
    }

    public async Task<bool> UpdateAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Bills
            SET account_id = @AccountId, category_id = @CategoryId,
                recurrence_id = @RecurrenceId, description = @Description,
                amount = @Amount, kind = @Kind, due_date = @DueDate, status = @Status,
                paid_amount = @PaidAmount, paid_at = @PaidAt,
                paid_transaction_id = @PaidTransactionId,
                installment_current = @InstallmentCurrent,
                installment_total = @InstallmentTotal,
                notes = @Notes, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, bill, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_Bills WHERE id = @Id AND user_id = @UserId";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
