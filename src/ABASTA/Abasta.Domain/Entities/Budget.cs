namespace Abasta.Domain.Entities;

/// <summary>
/// Orçamento mensal. O escopo é definido pelos FKs: ambos nulos = empresa;
/// user_id = colaborador; vehicle_id = veículo.
/// </summary>
public sealed class Budget
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTime ReferenceMonth { get; set; }
    public decimal AmountLimit { get; set; }
    public byte AlertThreshold { get; set; } = 80;
    public bool Rollover { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
