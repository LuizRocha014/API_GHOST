using Application.Agenda;
using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Equipe (usuários da agenda), o vínculo com as filiais e o cadastro das filiais da empresa.</summary>
public interface ITeamRepository
{
    /// <summary>Nulo se o usuário não existe. Sem <paramref name="branchId"/>, <c>BranchAllowed</c> é verdadeiro.</summary>
    Task<AgendaUserAccess?> GetAccessAsync(Guid userId, Guid? branchId, CancellationToken ct = default);

    Task<IReadOnlyList<TeamMemberRow>> ListMembersAsync(CancellationToken ct = default);

    /// <summary>Vínculos ativos de usuário com filial.</summary>
    Task<IReadOnlyList<(Guid UserId, Guid BranchId)>> ListMemberBranchesAsync(CancellationToken ct = default);

    /// <summary>Usuário por id, ativo ou não.</summary>
    Task<User?> GetUserAsync(Guid id, CancellationToken ct = default);

    Task<int> CountActiveOwnersAsync(Guid? exceptUserId, CancellationToken ct = default);

    /// <summary>Grava o usuário (cria se não existir) e troca as filiais dele, numa transação.</summary>
    Task SaveMemberAsync(User user, IReadOnlyList<Guid> branchIds, bool isNew, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyBranchDto>> ListCompanyBranchesAsync(Guid companyId, CancellationToken ct = default);
    Task<Guid?> CompanyOfBranchAsync(Guid branchId, CancellationToken ct = default);
    Task<Guid> CreateBranchAsync(Guid companyId, string name, CancellationToken ct = default);
    Task<bool> RenameBranchAsync(Guid companyId, Guid branchId, string name, CancellationToken ct = default);

    /// <summary>Tira a filial da lista (os dados da agenda dela continuam no banco).</summary>
    Task<bool> DeactivateBranchAsync(Guid companyId, Guid branchId, CancellationToken ct = default);
    Task RenameCompanyAsync(Guid companyId, string name, CancellationToken ct = default);
}
