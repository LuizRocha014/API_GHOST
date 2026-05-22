namespace Application.Companies;

public sealed class UpdateCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
