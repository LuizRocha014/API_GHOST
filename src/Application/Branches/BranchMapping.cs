using Domain.Entities;

namespace Application.Branches;

internal static class BranchMapping
{
    public static BranchDto ToDto(this Branch branch) =>
        new(branch.Id, branch.CompanyId, branch.Name, branch.Active, branch.CreatedAt, branch.UpdatedAt);
}
