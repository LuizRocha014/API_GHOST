namespace Domain.Entities;

public sealed class UserRole : Core
{
    public Guid UserCompanyBranchId { get; set; }
    public Guid RoleId { get; set; }
}
