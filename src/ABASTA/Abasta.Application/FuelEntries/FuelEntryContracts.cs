namespace Abasta.Application.FuelEntries;

public sealed record FuelEntryDto(
    Guid Id,
    Guid CompanyId,
    Guid UserId,
    Guid VehicleId,
    int FuelTypeId,
    Guid? StationId,
    Guid? ReceiptId,
    string? StationName,
    decimal Liters,
    decimal PricePerLiter,
    decimal TotalAmount,
    int? OdometerKm,
    DateTime FueledAt,
    string Status,
    string Source,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateFuelEntryRequest(
    Guid UserId,
    Guid VehicleId,
    int FuelTypeId,
    decimal Liters,
    decimal PricePerLiter,
    decimal TotalAmount,
    DateTime FueledAt,
    Guid? StationId = null,
    string? StationName = null,
    Guid? ReceiptId = null,
    int? OdometerKm = null,
    string Source = "manual",
    string? Notes = null,
    // Id opcional gerado pelo cliente (offline-first).
    Guid? Id = null);

public sealed record UpdateFuelEntryRequest(
    int FuelTypeId,
    decimal Liters,
    decimal PricePerLiter,
    decimal TotalAmount,
    DateTime FueledAt,
    Guid? StationId,
    string? StationName,
    int? OdometerKm,
    string Status,
    string? Notes);
