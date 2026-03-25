using Application.UserBranchAccesses;
using Domain.Entities;

namespace Application.Abstractions;

public interface IUserCompanyBranchRepository
{
    Task<UserBranchAccessDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserBranchAccessDto>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserCompanyBranch?> GetEntityByUserBranchAndAccessAsync(
        Guid userId,
        Guid branchId,
        Guid accessId,
        CancellationToken cancellationToken = default);
    Task<UserCompanyBranch> AddAsync(UserCompanyBranch access, CancellationToken cancellationToken = default);
    Task ReactivateAsync(Guid id, Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task UpdateActiveAsync(Guid id, Guid userId, bool active, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
