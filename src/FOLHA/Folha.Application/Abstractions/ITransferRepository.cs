using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ITransferRepository
{
    Task<IReadOnlyList<Transfer>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Transfer?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Transfer> AddAsync(Transfer transfer, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Transfer transfer, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
