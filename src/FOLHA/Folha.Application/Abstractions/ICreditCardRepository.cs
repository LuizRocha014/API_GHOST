using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ICreditCardRepository
{
    Task<IReadOnlyList<CreditCard>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCard?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCard> AddAsync(CreditCard card, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CreditCard card, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
