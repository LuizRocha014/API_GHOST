namespace Abasta.Application.Vehicles;

public sealed record VehicleDto(
    Guid Id,
    Guid CompanyId,
    Guid? FuelAccountId,
    int? DefaultFuelTypeId,
    string Plate,
    string? Label,
    string? Make,
    string? Model,
    short? ModelYear,
    decimal? TankCapacityLiters,
    int? CurrentOdometerKm,
    decimal? MonthlyBudget,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateVehicleRequest(
    string Plate,
    string? Label = null,
    string? Make = null,
    string? Model = null,
    short? ModelYear = null,
    int? DefaultFuelTypeId = null,
    Guid? FuelAccountId = null,
    decimal? TankCapacityLiters = null,
    int? CurrentOdometerKm = null,
    decimal? MonthlyBudget = null,
    Guid? Id = null);

public sealed record UpdateVehicleRequest(
    string Plate,
    string? Label,
    string? Make,
    string? Model,
    short? ModelYear,
    int? DefaultFuelTypeId,
    Guid? FuelAccountId,
    decimal? TankCapacityLiters,
    int? CurrentOdometerKm,
    decimal? MonthlyBudget,
    bool IsActive);
