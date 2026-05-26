using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class CreditCardInstallmentRepository : ICreditCardInstallmentRepository
{
    private readonly SqlSession _session;

    public CreditCardInstallmentRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, credit_card_id AS CreditCardId, description AS Description,
        installment_total AS InstallmentTotal, installments_paid AS InstallmentsPaid,
        installment_amount AS InstallmentAmount, start_date AS StartDate,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<CreditCardInstallment>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCardInstallments WHERE user_id = @UserId ORDER BY created_at";
        var list = await _session.Connection
            .QueryAsync<CreditCardInstallment>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<IReadOnlyList<CreditCardInstallment>> GetAllByCardAsync(Guid creditCardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCardInstallments WHERE credit_card_id = @CreditCardId AND user_id = @UserId ORDER BY created_at";
        var list = await _session.Connection
            .QueryAsync<CreditCardInstallment>(new CommandDefinition(sql, new { CreditCardId = creditCardId, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<CreditCardInstallment?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_CreditCardInstallments WHERE id = @Id AND user_id = @UserId";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<CreditCardInstallment>(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<CreditCardInstallment> AddAsync(CreditCardInstallment installment, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_CreditCardInstallments
                (id, user_id, credit_card_id, description, installment_total,
                 installments_paid, installment_amount, start_date, created_at, updated_at)
            VALUES
                (@Id, @UserId, @CreditCardId, @Description, @InstallmentTotal,
                 @InstallmentsPaid, @InstallmentAmount, @StartDate, @CreatedAt, @UpdatedAt)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, installment, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return installment;
    }

    public async Task<bool> UpdateAsync(CreditCardInstallment installment, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_CreditCardInstallments
            SET description = @Description, installment_total = @InstallmentTotal,
                installments_paid = @InstallmentsPaid, installment_amount = @InstallmentAmount,
                start_date = @StartDate, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, installment, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FOLHA_CreditCardInstallments WHERE id = @Id AND user_id = @UserId";
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
