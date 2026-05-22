namespace Folha.Application.Transfers;

public interface ITransferService
{
    Task<IReadOnlyList<TransferDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TransferDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<TransferDto> CreateAsync(Guid userId, CreateTransferRequest request, CancellationToken cancellationToken = default);
    Task<TransferDto?> UpdateAsync(Guid id, Guid userId, UpdateTransferRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
