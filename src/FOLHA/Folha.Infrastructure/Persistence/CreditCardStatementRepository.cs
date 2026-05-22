using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class CreditCardStatementRepository : ICreditCardStatementRepository
{
    private readonly SqlSession _session;

    public CreditCardStatementRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, credit_card_id AS CreditCardId, reference_month AS ReferenceMonth,
        closing_date AS ClosingDate, due_date AS DueDate, total_amount AS TotalAmount,
        paid_amount AS PaidAmount, status AS Status, paid_at AS PaidAt,
        paid_from_account_id AS PaidFromAccountId, created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<CreditCardStatement>> GetAllByCardAsync(Guid creditCardId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCardStatements WHERE credit_card_id = @CardId ORDER BY reference_month DESC";
        var list = await _session.Connection
            .QueryAsync<CreditCardStatement>(new CommandDefinition(sql, new { CardId = creditCardId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<CreditCardStatement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCardStatements WHERE id = @Id";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<CreditCardStatement>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<CreditCardStatement> AddAsync(CreditCardStatement statement, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_CreditCardStatements
                (id, credit_card_id, reference_month, closing_date, due_date, total_amount,
                 paid_amount, status, paid_at, paid_from_account_id, created_at, updated_at)
            VALUES
                (@Id, @CreditCardId, @ReferenceMonth, @ClosingDate, @DueDate, @TotalAmount,
                 @PaidAmount, @Status, @PaidAt, @PaidFromAccountId, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, statement, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return statement;
    }

    public async Task<bool> UpdateAsync(CreditCardStatement statement, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_CreditCardStatements
            SET closing_date = @ClosingDate, due_date = @DueDate, total_amount = @TotalAmount,
                paid_amount = @PaidAmount, status = @Status, paid_at = @PaidAt,
                paid_from_account_id = @PaidFromAccountId, updated_at = @UpdatedAt
            WHERE id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, statement, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_CreditCardStatements WHERE id = @Id";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
