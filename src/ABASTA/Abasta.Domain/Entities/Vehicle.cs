namespace Abasta.Domain.Entities;

/// <summary>Veículo da frota.</summary>
public sealed class Vehicle
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? FuelAccountId { get; set; }
    public int? DefaultFuelTypeId { get; set; }
    public string Plate { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public short? ModelYear { get; set; }
    public decimal? TankCapacityLiters { get; set; }
    public int? CurrentOdometerKm { get; set; }
    public decimal? MonthlyBudget { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
