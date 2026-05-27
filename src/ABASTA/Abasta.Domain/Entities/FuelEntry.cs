namespace Abasta.Domain.Entities;

/// <summary>Nota de abastecimento — o registro central do app.</summary>
public sealed class FuelEntry
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public Guid VehicleId { get; set; }
    public int FuelTypeId { get; set; }
    public Guid? StationId { get; set; }
    public Guid? ReceiptId { get; set; }
    public string? StationName { get; set; }
    public decimal Liters { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal TotalAmount { get; set; }
    public int? OdometerKm { get; set; }
    public DateTime FueledAt { get; set; }
    public string Status { get; set; } = "registered";
    public string Source { get; set; } = "manual";
    public string? Notes { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
