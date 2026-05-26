namespace Folha.Application.CreditCardInstallments;

public interface ICreditCardInstallmentService
{
    Task<IReadOnlyList<CreditCardInstallmentDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCardInstallmentDto>> ListByCardAsync(Guid creditCardId, Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCardInstallmentDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCardInstallmentDto> CreateAsync(Guid userId, CreateCreditCardInstallmentRequest request, CancellationToken cancellationToken = default);
    Task<CreditCardInstallmentDto?> UpdateAsync(Guid id, Guid userId, UpdateCreditCardInstallmentRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
