using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IRecurrenceRepository
{
    Task<IReadOnlyList<Recurrence>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Recurrence?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Recurrence> AddAsync(Recurrence recurrence, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Recurrence recurrence, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
