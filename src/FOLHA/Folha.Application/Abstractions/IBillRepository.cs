using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IBillRepository
{
    Task<IReadOnlyList<Bill>> GetAllByUserAsync(Guid userId, DateTime? modifiedSince = null, CancellationToken cancellationToken = default);
    Task<Bill?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Bill> AddAsync(Bill bill, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Bill bill, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
