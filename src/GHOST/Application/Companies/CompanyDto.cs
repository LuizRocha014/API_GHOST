namespace Application.Companies;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string Cnpj,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt);
