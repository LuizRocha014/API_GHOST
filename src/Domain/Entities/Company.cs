namespace Domain.Entities;

public sealed class Company : Core
{
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
}
