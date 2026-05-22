namespace Application.Branches;

public sealed record CreateBranchRequest(Guid CompanyId, string Name);
