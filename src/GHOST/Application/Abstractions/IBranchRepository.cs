using Domain.Entities;

namespace Application.Abstractions;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<Branch> AddAsync(Branch branch, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Branch branch, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
