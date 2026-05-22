using Folha.Domain.Entities;

namespace Folha.Application.Budgets;

internal static class BudgetMapping
{
    public static BudgetDto ToDto(this Budget b) => new(
        b.Id, b.UserId, b.CategoryId, b.ReferenceMonth, b.AmountLimit,
        b.Rollover, b.AlertThreshold, b.CreatedAt, b.UpdatedAt);
}
