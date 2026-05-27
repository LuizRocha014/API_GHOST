using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface IFuelEntryRepository
{
    /// <summary>Lista paginada e filtrável (escopo da empresa; opcionalmente por usuário/veículo).</summary>
    Task<IReadOnlyList<FuelEntry>> GetAllByCompanyAsync(
        Guid companyId,
        Guid? userId,
        Guid? vehicleId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<FuelEntry?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<FuelEntry> AddAsync(FuelEntry entry, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(FuelEntry entry, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}
