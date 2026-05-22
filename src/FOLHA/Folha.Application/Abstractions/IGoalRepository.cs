using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IGoalRepository
{
    Task<IReadOnlyList<Goal>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Goal> AddAsync(Goal goal, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Goal goal, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
