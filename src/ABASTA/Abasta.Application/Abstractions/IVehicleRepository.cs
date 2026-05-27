using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface IVehicleRepository
{
    Task<IReadOnlyList<Vehicle>> GetAllByCompanyAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<Vehicle> AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}
