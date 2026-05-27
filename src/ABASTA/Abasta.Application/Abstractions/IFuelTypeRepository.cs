using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface IFuelTypeRepository
{
    Task<IReadOnlyList<FuelType>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}
