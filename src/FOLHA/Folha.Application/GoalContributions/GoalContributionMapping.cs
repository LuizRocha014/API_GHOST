using Folha.Domain.Entities;

namespace Folha.Application.GoalContributions;

internal static class GoalContributionMapping
{
    public static GoalContributionDto ToDto(this GoalContribution gc) => new(
        gc.Id, gc.GoalId, gc.TransactionId, gc.Amount, gc.ContributedAt, gc.Note,
        gc.CreatedAt, gc.UpdatedAt);
}
