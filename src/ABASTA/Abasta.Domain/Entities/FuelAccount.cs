namespace Abasta.Domain.Entities;

/// <summary>Conta da empresa numa distribuidora (ex.: "BR Distribuidora").</summary>
public sealed class FuelAccount
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Distributor { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
