using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(Guid id, CancellationToken cancellationToken = default);
}
