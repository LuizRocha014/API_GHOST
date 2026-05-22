using Application.Abstractions;
using Domain.Entities;

namespace Application.Branches;

public sealed class BranchService : IBranchService
{
    private readonly IBranchRepository _branches;
    private readonly ICompanyRepository _companies;

    public BranchService(IBranchRepository branches, ICompanyRepository companies)
    {
        _branches = branches;
        _companies = companies;
    }

    public async Task<IReadOnlyList<BranchDto>> ListAsync(Guid? companyId, CancellationToken cancellationToken = default)
    {
        if (companyId.HasValue)
        {
            var list = await _branches.ListByCompanyAsync(companyId.Value, cancellationToken).ConfigureAwait(false);
            return list.Select(b => b.ToDto()).ToList();
        }

        var all = await _branches.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Select(b => b.ToDto()).ToList();
    }

    public async Task<BranchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return branch?.ToDto();
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var company = await _companies.GetByIdAsync(request.CompanyId, cancellationToken).ConfigureAwait(false)
                      ?? throw new InvalidOperationException("Empresa não encontrada.");

        var utc = DateTime.UtcNow;
        var entity = new Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            Name = request.Name.Trim(),
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        var created = await _branches.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<BranchDto?> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (branch is null)
            return null;

        branch.Name = request.Name.Trim();
        branch.Active = request.Active;
        branch.UpdatedAt = DateTime.UtcNow;

        var ok = await _branches.UpdateAsync(branch, cancellationToken).ConfigureAwait(false);
        return ok ? branch.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _branches.SoftDeleteAsync(id, cancellationToken);
}
