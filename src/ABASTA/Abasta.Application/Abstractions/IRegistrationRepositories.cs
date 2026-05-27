using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface IStationRepository
{
    Task<IReadOnlyList<Station>> GetAllByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Station?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<Station> AddAsync(Station station, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Station station, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}

public interface IFuelAccountRepository
{
    Task<IReadOnlyList<FuelAccount>> GetAllByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<FuelAccount?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<FuelAccount> AddAsync(FuelAccount account, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(FuelAccount account, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}
