using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IGoalContributionRepository
{
    Task<IReadOnlyList<GoalContribution>> GetAllByGoalAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<GoalContribution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoalContribution> AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(GoalContribution contribution, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
