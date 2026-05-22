using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ITransactionRepository
{
    Task<IReadOnlyList<Transaction>> GetAllByUserAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
