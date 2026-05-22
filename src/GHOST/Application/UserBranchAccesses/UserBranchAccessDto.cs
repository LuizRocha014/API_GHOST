namespace Application.UserBranchAccesses;

/// <summary>Vínculo usuário–empresa–filial.</summary>
public sealed record UserBranchAccessDto(
    Guid Id,
    Guid UserId,
    Guid CompanyId,
    Guid BranchId,
    Guid AccessId,
    string AccessName,
    string? AccessCode,
    string BranchName,
    string CompanyName,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt);
