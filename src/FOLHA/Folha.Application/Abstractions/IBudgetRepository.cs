using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IBudgetRepository
{
    Task<IReadOnlyList<Budget>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Budget?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Budget> AddAsync(Budget budget, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Budget budget, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
