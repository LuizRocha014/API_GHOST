namespace Folha.Application.Transactions;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionDto>> ListAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<TransactionDto> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<TransactionDto?> UpdateAsync(Guid id, Guid userId, UpdateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
