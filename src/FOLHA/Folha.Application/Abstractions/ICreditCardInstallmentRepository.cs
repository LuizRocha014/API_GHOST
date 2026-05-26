using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ICreditCardInstallmentRepository
{
    Task<IReadOnlyList<CreditCardInstallment>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCardInstallment>> GetAllByCardAsync(Guid creditCardId, Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCardInstallment?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCardInstallment> AddAsync(CreditCardInstallment installment, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CreditCardInstallment installment, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
