using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Account> AddAsync(Account account, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Account account, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
