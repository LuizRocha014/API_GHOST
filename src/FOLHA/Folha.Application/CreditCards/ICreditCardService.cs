namespace Folha.Application.CreditCards;

public interface ICreditCardService
{
    Task<IReadOnlyList<CreditCardDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCardDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CreditCardDto> CreateAsync(Guid userId, CreateCreditCardRequest request, CancellationToken cancellationToken = default);
    Task<CreditCardDto?> UpdateAsync(Guid id, Guid userId, UpdateCreditCardRequest request, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
