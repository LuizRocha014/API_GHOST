namespace Abasta.Domain.Entities;

/// <summary>Empresa/cliente — tenant raiz. Todo dado de negócio é escopado por ela.</summary>
public sealed class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Cnpj { get; set; }
    public decimal? MonthlyBudget { get; set; }
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public string CurrencyCode { get; set; } = "BRL";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
