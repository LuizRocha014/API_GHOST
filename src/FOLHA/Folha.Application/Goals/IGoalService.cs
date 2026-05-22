namespace Folha.Application.Goals;

public interface IGoalService
{
    Task<IReadOnlyList<GoalDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GoalDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<GoalDto> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task<GoalDto?> UpdateAsync(Guid id, Guid userId, UpdateGoalRequest request, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
