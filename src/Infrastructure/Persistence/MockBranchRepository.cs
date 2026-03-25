using Application.Abstractions;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class MockBranchRepository : IBranchRepository
{
    private readonly List<Branch> _store;

    public MockBranchRepository()
    {
        var utc = DateTime.UtcNow;
        var companyId = MockSeedIds.CompanyId;
        _store =
        [
            new Branch
            {
                Id = MockSeedIds.BranchCentroId,
                CompanyId = companyId,
                Name = "Filial Centro",
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            },
            new Branch
            {
                Id = MockSeedIds.BranchSulId,
                CompanyId = companyId,
                Name = "Filial Zona Sul",
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }
        ];
    }

    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var b = _store.FirstOrDefault(x => x.Id == id && x.Active);
        return Task.FromResult(b);
    }

    public Task<IReadOnlyList<Branch>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Branch> list = _store.Where(x => x.CompanyId == companyId && x.Active).OrderBy(x => x.Name).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Branch>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Branch> list = _store.Where(x => x.Active).OrderBy(x => x.Name).ToList();
        return Task.FromResult(list);
    }

    public Task<Branch> AddAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        _store.Add(branch);
        return Task.FromResult(branch);
    }

    public Task<bool> UpdateAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        var index = _store.FindIndex(b => b.Id == branch.Id);
        if (index < 0)
            return Task.FromResult(false);

        _store[index] = branch;
        return Task.FromResult(true);
    }

    public Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var b = _store.FirstOrDefault(x => x.Id == id);
        if (b is null || !b.Active)
            return Task.FromResult(b is not null);

        b.Active = false;
        b.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }
}
