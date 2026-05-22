namespace Application.UserBranchAccesses;

/// <summary>Dados para incluir acesso a uma filial.</summary>
public sealed record CreateUserBranchAccessRequest(Guid BranchId, Guid AccessId, Guid? CompanyId = null);
