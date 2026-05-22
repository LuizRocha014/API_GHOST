using Application.Abstractions;
using Domain.Entities;

namespace Application.Companies;

public sealed class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<CompanyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(c => c.ToDto()).ToList();
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(id, cancellationToken);
        return company?.ToDto();
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        var entity = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Cnpj = request.Cnpj.Trim(),
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        return created.ToDto();
    }

    public async Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var company = await _repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
            return null;

        company.Name = request.Name.Trim();
        company.Cnpj = request.Cnpj.Trim();
        company.Active = request.Active;
        company.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(company, cancellationToken);
        return ok ? company.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.SoftDeleteAsync(id, cancellationToken);
}
