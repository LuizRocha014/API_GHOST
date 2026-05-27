using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;

namespace Abasta.Application.Vehicles;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleDto>> ListAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);
    Task<VehicleDto?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<VehicleDto> CreateAsync(Guid companyId, CreateVehicleRequest request, CancellationToken cancellationToken = default);
    Task<VehicleDto?> UpdateAsync(Guid id, Guid companyId, UpdateVehicleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Vincula um veículo a um motorista (vínculo primário).</summary>
    Task<bool> AssignDriverAsync(Guid companyId, Guid vehicleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Veículo que o usuário dirige atualmente, se houver.</summary>
    Task<VehicleDto?> GetMyVehicleAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _repository;
    private readonly IVehicleAssignmentRepository _assignments;

    public VehicleService(IVehicleRepository repository, IVehicleAssignmentRepository assignments)
    {
        _repository = repository;
        _assignments = assignments;
    }

    public async Task<IReadOnlyList<VehicleDto>> ListAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (take <= 0 || take > 500) take = 100;
        var items = await _repository.GetAllByCompanyAsync(companyId, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<VehicleDto?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var v = await _repository.GetByIdAsync(id, companyId, cancellationToken).ConfigureAwait(false);
        return v is null ? null : Map(v);
    }

    public async Task<VehicleDto> CreateAsync(Guid companyId, CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        var plate = NormalizePlate(request.Plate);
        if (plate.Length == 0)
            throw new InvalidOperationException("Placa é obrigatória.");

        var utc = DateTime.UtcNow;
        var entity = new Vehicle
        {
            Id = request.Id ?? Guid.NewGuid(),
            CompanyId = companyId,
            FuelAccountId = request.FuelAccountId,
            DefaultFuelTypeId = request.DefaultFuelTypeId,
            Plate = plate,
            Label = Trim(request.Label),
            Make = Trim(request.Make),
            Model = Trim(request.Model),
            ModelYear = request.ModelYear,
            TankCapacityLiters = request.TankCapacityLiters,
            CurrentOdometerKm = request.CurrentOdometerKm,
            MonthlyBudget = request.MonthlyBudget,
            IsActive = true,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return Map(created);
    }

    public async Task<VehicleDto?> UpdateAsync(Guid id, Guid companyId, UpdateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, companyId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;

        entity.Plate = NormalizePlate(request.Plate);
        entity.Label = Trim(request.Label);
        entity.Make = Trim(request.Make);
        entity.Model = Trim(request.Model);
        entity.ModelYear = request.ModelYear;
        entity.DefaultFuelTypeId = request.DefaultFuelTypeId;
        entity.FuelAccountId = request.FuelAccountId;
        entity.TankCapacityLiters = request.TankCapacityLiters;
        entity.CurrentOdometerKm = request.CurrentOdometerKm;
        entity.MonthlyBudget = request.MonthlyBudget;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? Map(entity) : null;
    }

    public Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default) =>
        _repository.DeactivateAsync(id, companyId, cancellationToken);

    public async Task<bool> AssignDriverAsync(Guid companyId, Guid vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        var vehicle = await _repository.GetByIdAsync(vehicleId, companyId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null) return false;
        await _assignments.AssignPrimaryAsync(companyId, vehicleId, userId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<VehicleDto?> GetMyVehicleAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        var vehicleId = await _assignments.GetActiveVehicleIdAsync(companyId, userId, cancellationToken).ConfigureAwait(false);
        if (vehicleId is null) return null;
        var v = await _repository.GetByIdAsync(vehicleId.Value, companyId, cancellationToken).ConfigureAwait(false);
        return v is null ? null : Map(v);
    }

    private static string NormalizePlate(string plate) =>
        new string((plate ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static VehicleDto Map(Vehicle v) => new(
        v.Id, v.CompanyId, v.FuelAccountId, v.DefaultFuelTypeId, v.Plate, v.Label,
        v.Make, v.Model, v.ModelYear, v.TankCapacityLiters, v.CurrentOdometerKm,
        v.MonthlyBudget, v.IsActive, v.CreatedAt, v.UpdatedAt);
}
