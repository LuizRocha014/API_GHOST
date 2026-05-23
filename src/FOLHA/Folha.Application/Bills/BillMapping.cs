using Folha.Domain.Entities;

namespace Folha.Application.Bills;

internal static class BillMapping
{
    public static BillDto ToDto(this Bill b) => new(
        b.Id, b.UserId, b.AccountId, b.CategoryId, b.RecurrenceId,
        b.Description, b.Amount, b.Kind, b.DueDate, b.Status,
        b.PaidAmount, b.PaidAt, b.PaidTransactionId,
        b.InstallmentCurrent, b.InstallmentTotal,
        b.Notes, b.CreatedAt, b.UpdatedAt);
}
