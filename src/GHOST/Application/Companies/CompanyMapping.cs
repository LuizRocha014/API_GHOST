using Domain.Entities;

namespace Application.Companies;

internal static class CompanyMapping
{
    public static CompanyDto ToDto(this Company company) =>
        new(company.Id, company.Name, company.Cnpj, company.Active, company.CreatedAt, company.UpdatedAt);
}
