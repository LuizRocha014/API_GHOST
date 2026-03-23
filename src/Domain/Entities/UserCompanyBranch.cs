namespace Domain.Entities;

public sealed class UserCompanyBranch : Core
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
}
