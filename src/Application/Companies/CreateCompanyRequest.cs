namespace Application.Companies;

public sealed class CreateCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
}
