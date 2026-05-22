using Folha.Domain.Entities;

namespace Folha.Application.CreditCardStatements;

internal static class CreditCardStatementMapping
{
    public static CreditCardStatementDto ToDto(this CreditCardStatement s) => new(
        s.Id, s.CreditCardId, s.ReferenceMonth, s.ClosingDate, s.DueDate,
        s.TotalAmount, s.PaidAmount, s.Status, s.PaidAt, s.PaidFromAccountId,
        s.CreatedAt, s.UpdatedAt);
}
