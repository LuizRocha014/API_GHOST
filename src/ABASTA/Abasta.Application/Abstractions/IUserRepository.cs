using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkEmailVerifiedAsync(Guid id, CancellationToken cancellationToken = default);
}
