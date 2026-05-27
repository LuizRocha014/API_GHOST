using Abasta.Application.Abstractions;

namespace Abasta.Application.FuelTypes;

public interface IFuelTypeService
{
    Task<IReadOnlyList<FuelTypeDto>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class FuelTypeService : IFuelTypeService
{
    private readonly IFuelTypeRepository _repository;

    public FuelTypeService(IFuelTypeRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<FuelTypeDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
        return items
            .Select(f => new FuelTypeDto(f.Id, f.Code, f.Label, f.ColorHex, f.SortOrder))
            .ToList();
    }
}
