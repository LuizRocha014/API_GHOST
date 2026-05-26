using Folha.Domain.Entities;

namespace Folha.Application.CreditCardInstallments;

internal static class CreditCardInstallmentMapping
{
    public static CreditCardInstallmentDto ToDto(this CreditCardInstallment i) => new(
        i.Id, i.UserId, i.CreditCardId, i.Description, i.InstallmentTotal,
        i.InstallmentsPaid, i.InstallmentAmount, i.StartDate, i.CreatedAt, i.UpdatedAt);
}
