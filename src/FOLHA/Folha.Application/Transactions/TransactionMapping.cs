using Folha.Domain.Entities;

namespace Folha.Application.Transactions;

internal static class TransactionMapping
{
    public static TransactionDto ToDto(this Transaction t) => new(
        t.Id, t.UserId, t.AccountId, t.CreditCardId, t.CreditCardStatementId,
        t.CategoryId, t.BillId, t.RecurrenceId, t.ParentTransactionId, t.TransferId,
        t.Description, t.Place, t.Notes, t.Amount, t.Kind, t.OccurredAt,
        t.InstallmentNumber, t.InstallmentTotal, t.IsPending, t.IsExcludedFromReports,
        t.CreatedAt, t.UpdatedAt);
}
