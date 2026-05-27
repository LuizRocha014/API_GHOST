using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;

namespace Abasta.Application.FuelEntries;

public interface IFuelEntryService
{
    Task<IReadOnlyList<FuelEntryDto>> ListAsync(Guid companyId, Guid? userId, Guid? vehicleId, int skip, int take, CancellationToken cancellationToken = default);
    Task<FuelEntryDto?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<FuelEntryDto> CreateAsync(Guid companyId, CreateFuelEntryRequest request, CancellationToken cancellationToken = default);
    Task<FuelEntryDto?> UpdateAsync(Guid id, Guid companyId, UpdateFuelEntryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class FuelEntryService : IFuelEntryService
{
    private static readonly HashSet<string> ValidSources =
        new(StringComparer.OrdinalIgnoreCase) { "manual", "ocr", "import" };
    private static readonly HashSet<string> ValidStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "registered", "pending_review", "rejected" };

    private readonly IFuelEntryRepository _repository;

    public FuelEntryService(IFuelEntryRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<FuelEntryDto>> ListAsync(Guid companyId, Guid? userId, Guid? vehicleId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (take <= 0 || take > 500) take = 50;
        var items = await _repository.GetAllByCompanyAsync(companyId, userId, vehicleId, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<FuelEntryDto?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var e = await _repository.GetByIdAsync(id, companyId, cancellationToken).ConfigureAwait(false);
        return e is null ? null : Map(e);
    }

    public async Task<FuelEntryDto> CreateAsync(Guid companyId, CreateFuelEntryRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Liters, request.PricePerLiter, request.TotalAmount, request.OdometerKm, request.Source);

        var utc = DateTime.UtcNow;
        var entity = new FuelEntry
        {
            Id = request.Id ?? Guid.NewGuid(),
            CompanyId = companyId,
            UserId = request.UserId,
            VehicleId = request.VehicleId,
            FuelTypeId = request.FuelTypeId,
            StationId = request.StationId,
            ReceiptId = request.ReceiptId,
            StationName = Trim(request.StationName),
            Liters = request.Liters,
            PricePerLiter = request.PricePerLiter,
            TotalAmount = request.TotalAmount,
            OdometerKm = request.OdometerKm,
            FueledAt = request.FueledAt,
            Status = "registered",
            Source = request.Source.Trim().ToLowerInvariant(),
            Notes = Trim(request.Notes),
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return Map(created);
    }

    public async Task<FuelEntryDto?> UpdateAsync(Guid id, Guid companyId, UpdateFuelEntryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, companyId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;

        Validate(request.Liters, request.PricePerLiter, request.TotalAmount, request.OdometerKm, entity.Source);
        if (!ValidStatuses.Contains(request.Status))
            throw new InvalidOperationException("Status inválido.");

        entity.FuelTypeId = request.FuelTypeId;
        entity.StationId = request.StationId;
        entity.StationName = Trim(request.StationName);
        entity.Liters = request.Liters;
        entity.PricePerLiter = request.PricePerLiter;
        entity.TotalAmount = request.TotalAmount;
        entity.OdometerKm = request.OdometerKm;
        entity.FueledAt = request.FueledAt;
        entity.Status = request.Status.Trim().ToLowerInvariant();
        entity.Notes = Trim(request.Notes);
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? Map(entity) : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default) =>
        _repository.SoftDeleteAsync(id, companyId, cancellationToken);

    private static void Validate(decimal liters, decimal pricePerLiter, decimal total, int? odometer, string source)
    {
        if (liters <= 0) throw new InvalidOperationException("Litros precisa ser positivo.");
        if (pricePerLiter < 0) throw new InvalidOperationException("Preço por litro não pode ser negativo.");
        if (total <= 0) throw new InvalidOperationException("Total precisa ser positivo.");
        if (odometer is < 0) throw new InvalidOperationException("KM não pode ser negativo.");
        if (!ValidSources.Contains(source)) throw new InvalidOperationException("Origem inválida.");
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static FuelEntryDto Map(FuelEntry e) => new(
        e.Id, e.CompanyId, e.UserId, e.VehicleId, e.FuelTypeId, e.StationId, e.ReceiptId,
        e.StationName, e.Liters, e.PricePerLiter, e.TotalAmount, e.OdometerKm, e.FueledAt,
        e.Status, e.Source, e.Notes, e.CreatedAt, e.UpdatedAt);
}
