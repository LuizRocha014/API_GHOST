namespace Folha.Application.GoalContributions;

public interface IGoalContributionService
{
    Task<IReadOnlyList<GoalContributionDto>> ListByGoalAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<GoalContributionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoalContributionDto> CreateAsync(CreateGoalContributionRequest request, CancellationToken cancellationToken = default);
    Task<GoalContributionDto?> UpdateAsync(Guid id, UpdateGoalContributionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
