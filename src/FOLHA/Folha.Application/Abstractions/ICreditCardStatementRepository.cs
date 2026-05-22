using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ICreditCardStatementRepository
{
    Task<IReadOnlyList<CreditCardStatement>> GetAllByCardAsync(Guid creditCardId, CancellationToken cancellationToken = default);
    Task<CreditCardStatement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CreditCardStatement> AddAsync(CreditCardStatement statement, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CreditCardStatement statement, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
