namespace Application.Companies;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<CompanyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
