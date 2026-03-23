using Application.Abstractions;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class MockCompanyRepository : ICompanyRepository
{
    private readonly List<Company> _store;

    public MockCompanyRepository()
    {
        var utc = DateTime.UtcNow;
        _store =
        [
            new Company
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Name = "Empresa Demo",
                Cnpj = "00000000000191",
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }
        ];
    }

    public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Company> list = _store.Where(c => c.Active).OrderBy(c => c.Name).ToList();
        return Task.FromResult(list);
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = _store.FirstOrDefault(c => c.Id == id && c.Active);
        return Task.FromResult(company);
    }

    public Task<Company> AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        _store.Add(company);
        return Task.FromResult(company);
    }

    public Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default)
    {
        var index = _store.FindIndex(c => c.Id == company.Id);
        if (index < 0)
            return Task.FromResult(false);

        _store[index] = company;
        return Task.FromResult(true);
    }
}
