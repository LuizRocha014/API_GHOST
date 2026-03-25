using Domain.Entities;

namespace Application.Abstractions;

public interface IAccessRepository
{
    Task<Access?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Access>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<Access> AddAsync(Access access, CancellationToken cancellationToken = default);
}
