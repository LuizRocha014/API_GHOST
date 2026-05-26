using Folha.Domain.Entities;

namespace Folha.Application.Goals;

internal static class GoalMapping
{
    public static GoalDto ToDto(this Goal g) => new(
        g.Id, g.UserId, g.AccountId, g.Title, g.Description, g.TargetAmount,
        g.CurrentAmount, g.TargetDate, g.Icon, g.ColorHex, g.IsCompleted,
        g.CompletedAt, g.IsArchived, g.MonthlyYieldPercent, g.IsCdb,
        g.CreatedAt, g.UpdatedAt);
}
