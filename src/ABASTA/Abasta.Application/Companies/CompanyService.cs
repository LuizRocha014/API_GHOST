using Abasta.Application.Abstractions;

namespace Abasta.Application.Companies;

public sealed record CompanyDto(
    Guid Id,
    String Name,
    String? LegalName,
    String? Cnpj,
    decimal? MonthlyBudget,
    String Timezone,
    String CurrencyCode);

public sealed record UpdateCompanyRequest(
    String Name,
    String? LegalName,
    String? Cnpj,
    decimal? MonthlyBudget);

public interface ICompanyService
{
    Task<CompanyDto?> GetAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyDto?> UpdateAsync(Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
}

public sealed class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository) => _repository = repository;

    public async Task<CompanyDto?> GetAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var c = await _repository.GetByIdAsync(companyId, cancellationToken).ConfigureAwait(false);
        return c is null ? null : Map(c);
    }

    public async Task<CompanyDto?> UpdateAsync(Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var c = await _repository.GetByIdAsync(companyId, cancellationToken).ConfigureAwait(false);
        if (c is null) return null;

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Nome da empresa é obrigatório.");

        c.Name = request.Name.Trim();
        c.LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim();
        c.Cnpj = request.Cnpj is null ? null : new string(request.Cnpj.Where(char.IsDigit).ToArray());
        if (c.Cnpj is { Length: 0 }) c.Cnpj = null;
        c.MonthlyBudget = request.MonthlyBudget;
        c.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(c, cancellationToken).ConfigureAwait(false);
        return Map(c);
    }

    private static CompanyDto Map(Domain.Entities.Company c) =>
        new(c.Id, c.Name, c.LegalName, c.Cnpj, c.MonthlyBudget, c.Timezone, c.CurrencyCode);
}
