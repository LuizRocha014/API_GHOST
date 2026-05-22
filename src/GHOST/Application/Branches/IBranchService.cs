namespace Application.Branches;

public interface IBranchService
{
    Task<IReadOnlyList<BranchDto>> ListAsync(Guid? companyId, CancellationToken cancellationToken = default);
    Task<BranchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto?> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
