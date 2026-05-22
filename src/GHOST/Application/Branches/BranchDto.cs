namespace Application.Branches;

public sealed record BranchDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt);
