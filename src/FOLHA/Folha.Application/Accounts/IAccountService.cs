namespace Folha.Application.Accounts;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AccountDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<AccountDto> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto?> UpdateAsync(Guid id, Guid userId, UpdateAccountRequest request, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
