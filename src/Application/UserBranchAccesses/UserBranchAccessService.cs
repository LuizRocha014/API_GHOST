using Application.Abstractions;
using Domain.Entities;

namespace Application.UserBranchAccesses;

public sealed class UserBranchAccessService : IUserBranchAccessService
{
    private readonly IUserCompanyBranchRepository _accesses;
    private readonly IUserRepository _users;
    private readonly IBranchRepository _branches;
    private readonly IAccessRepository _accessCatalog;

    public UserBranchAccessService(
        IUserCompanyBranchRepository accesses,
        IUserRepository users,
        IBranchRepository branches,
        IAccessRepository accessCatalog)
    {
        _accesses = accesses;
        _users = users;
        _branches = branches;
        _accessCatalog = accessCatalog;
    }

    public Task<IReadOnlyList<UserBranchAccessDto>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _accesses.ListByUserAsync(userId, cancellationToken);

    public Task<UserBranchAccessDto?> GetByIdAsync(Guid userId, Guid accessId, CancellationToken cancellationToken = default) =>
        _accesses.GetByIdAsync(accessId, userId, cancellationToken);

    public async Task<UserBranchAccessDto> CreateAsync(
        Guid userId,
        CreateUserBranchAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Usuário não encontrado ou inativo.");

        _ = await _accessCatalog.GetByIdAsync(request.AccessId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Tipo de acesso não encontrado ou inativo.");

        var branch = await _branches.GetByIdAsync(request.BranchId, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Filial não encontrada ou inativa.");

        if (request.CompanyId is { } explicitCompany && explicitCompany != branch.CompanyId)
            throw new InvalidOperationException("A filial não pertence à empresa informada.");

        var existing = await _accesses
            .GetEntityByUserBranchAndAccessAsync(userId, branch.Id, request.AccessId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Active)
                throw new InvalidOperationException("Este usuário já possui este acesso nesta filial.");

            await _accesses.ReactivateAsync(existing.Id, userId, branch.CompanyId, cancellationToken).ConfigureAwait(false);
            return await _accesses.GetByIdAsync(existing.Id, userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("Não foi possível carregar o acesso reativado.");
        }

        var utc = DateTime.UtcNow;
        var entity = new UserCompanyBranch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = branch.CompanyId,
            BranchId = branch.Id,
            AccessId = request.AccessId,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        await _accesses.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        var created = await _accesses.GetByIdAsync(entity.Id, userId, cancellationToken).ConfigureAwait(false);
        return created ?? throw new InvalidOperationException("Não foi possível carregar o acesso criado.");
    }

    public async Task<UserBranchAccessDto?> UpdateAsync(
        Guid userId,
        Guid accessId,
        UpdateUserBranchAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await _accesses.GetByIdAsync(accessId, userId, cancellationToken).ConfigureAwait(false);
        if (current is null)
            return null;

        await _accesses.UpdateActiveAsync(accessId, userId, request.Active, cancellationToken).ConfigureAwait(false);
        return await _accesses.GetByIdAsync(accessId, userId, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid accessId, CancellationToken cancellationToken = default) =>
        _accesses.SoftDeleteAsync(accessId, userId, cancellationToken);
}
