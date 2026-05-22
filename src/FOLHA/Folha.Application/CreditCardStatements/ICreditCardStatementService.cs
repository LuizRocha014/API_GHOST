namespace Folha.Application.CreditCardStatements;

public interface ICreditCardStatementService
{
    Task<IReadOnlyList<CreditCardStatementDto>> ListByCardAsync(Guid creditCardId, CancellationToken cancellationToken = default);
    Task<CreditCardStatementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CreditCardStatementDto> CreateAsync(CreateCreditCardStatementRequest request, CancellationToken cancellationToken = default);
    Task<CreditCardStatementDto?> UpdateAsync(Guid id, UpdateCreditCardStatementRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
