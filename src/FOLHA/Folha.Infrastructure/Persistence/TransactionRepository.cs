using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly SqlSession _session;

    public TransactionRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, account_id AS AccountId, credit_card_id AS CreditCardId,
        credit_card_statement_id AS CreditCardStatementId, category_id AS CategoryId,
        bill_id AS BillId, recurrence_id AS RecurrenceId,
        parent_transaction_id AS ParentTransactionId, transfer_id AS TransferId,
        description AS Description, place AS Place, notes AS Notes, amount AS Amount,
        kind AS Kind, occurred_at AS OccurredAt,
        installment_number AS InstallmentNumber, installment_total AS InstallmentTotal,
        is_pending AS IsPending, is_excluded_from_reports AS IsExcludedFromReports,
        deleted_at AS DeletedAt, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Transaction>> GetAllByUserAsync(Guid userId, int skip, int take, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columns} FROM FOLHA_Transactions
            WHERE user_id = @UserId AND deleted_at IS NULL
              AND (@ModifiedSince IS NULL OR updated_at > @ModifiedSince)
            ORDER BY occurred_at DESC, created_at DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """;
        var list = await _session.Connection
            .QueryAsync<Transaction>(new CommandDefinition(sql, new { UserId = userId, Skip = skip, Take = take, ModifiedSince = modifiedSince }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Transactions WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Transaction>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Transactions
                (id, user_id, account_id, credit_card_id, credit_card_statement_id,
                 category_id, bill_id, recurrence_id, parent_transaction_id, transfer_id,
                 description, place, notes, amount, kind, occurred_at,
                 installment_number, installment_total, is_pending, is_excluded_from_reports,
                 created_at, updated_at)
            VALUES
                (@Id, @UserId, @AccountId, @CreditCardId, @CreditCardStatementId,
                 @CategoryId, @BillId, @RecurrenceId, @ParentTransactionId, @TransferId,
                 @Description, @Place, @Notes, @Amount, @Kind, @OccurredAt,
                 @InstallmentNumber, @InstallmentTotal, @IsPending, @IsExcludedFromReports,
                 @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return transaction;
    }

    public async Task<bool> UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Transactions
            SET account_id = @AccountId, credit_card_id = @CreditCardId,
                credit_card_statement_id = @CreditCardStatementId, category_id = @CategoryId,
                description = @Description, place = @Place, notes = @Notes,
                amount = @Amount, kind = @Kind, occurred_at = @OccurredAt,
                installment_number = @InstallmentNumber, installment_total = @InstallmentTotal,
                is_pending = @IsPending, is_excluded_from_reports = @IsExcludedFromReports,
                updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Transactions SET deleted_at = @Now, updated_at = @Now
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, Now = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
