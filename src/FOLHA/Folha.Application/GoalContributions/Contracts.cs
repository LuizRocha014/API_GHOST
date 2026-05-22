namespace Folha.Application.GoalContributions;

public sealed record GoalContributionDto(
    Guid Id,
    Guid GoalId,
    Guid? TransactionId,
    decimal Amount,
    DateTime ContributedAt,
    string? Note,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateGoalContributionRequest(
    Guid GoalId,
    Guid? TransactionId,
    decimal Amount,
    DateTime ContributedAt,
    string? Note = null);

public sealed record UpdateGoalContributionRequest(
    Guid? TransactionId,
    decimal Amount,
    DateTime ContributedAt,
    string? Note);
