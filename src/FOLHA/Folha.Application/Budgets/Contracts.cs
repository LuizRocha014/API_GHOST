namespace Folha.Application.Budgets;

public sealed record BudgetDto(
    Guid Id,
    Guid UserId,
    int CategoryId,
    DateTime ReferenceMonth,
    decimal AmountLimit,
    bool Rollover,
    byte AlertThreshold,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateBudgetRequest(
    int CategoryId,
    DateTime ReferenceMonth,
    decimal AmountLimit,
    bool Rollover = false,
    byte AlertThreshold = 80);

public sealed record UpdateBudgetRequest(
    int CategoryId,
    DateTime ReferenceMonth,
    decimal AmountLimit,
    bool Rollover,
    byte AlertThreshold);
