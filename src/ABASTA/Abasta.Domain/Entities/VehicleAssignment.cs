namespace Abasta.Domain.Entities;

/// <summary>Vínculo (histórico) entre um veículo e o motorista que o dirige.</summary>
public sealed class VehicleAssignment
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
