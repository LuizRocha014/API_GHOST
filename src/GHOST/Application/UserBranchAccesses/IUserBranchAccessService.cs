namespace Application.UserBranchAccesses;

public interface IUserBranchAccessService
{
    Task<IReadOnlyList<UserBranchAccessDto>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserBranchAccessDto?> GetByIdAsync(Guid userId, Guid accessId, CancellationToken cancellationToken = default);
    Task<UserBranchAccessDto> CreateAsync(Guid userId, CreateUserBranchAccessRequest request, CancellationToken cancellationToken = default);
    Task<UserBranchAccessDto?> UpdateAsync(Guid userId, Guid accessId, UpdateUserBranchAccessRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid accessId, CancellationToken cancellationToken = default);
}
