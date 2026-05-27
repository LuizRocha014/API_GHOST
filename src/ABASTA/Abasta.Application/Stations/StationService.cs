using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;

namespace Abasta.Application.Stations;

public sealed record StationDto(
    Guid Id,
    Guid CompanyId,
    String Name,
    String? Brand,
    String? City,
    String? State,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateStationRequest(
    String Name,
    String? Brand = null,
    String? Cnpj = null,
    String? City = null,
    String? State = null,
    Guid? Id = null);

public sealed record UpdateStationRequest(String Name, String? Brand, String? City, String? State);

public interface IStationService
{
    Task<IReadOnlyList<StationDto>> ListAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<StationDto> CreateAsync(Guid companyId, CreateStationRequest request, CancellationToken cancellationToken = default);
    Task<StationDto?> UpdateAsync(Guid id, Guid companyId, UpdateStationRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class StationService : IStationService
{
    private readonly IStationRepository _repository;

    public StationService(IStationRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<StationDto>> ListAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByCompanyAsync(companyId, cancellationToken).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<StationDto> CreateAsync(Guid companyId, CreateStationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Nome do posto é obrigatório.");

        var utc = DateTime.UtcNow;
        var entity = await _repository.AddAsync(new Station
        {
            Id = request.Id ?? Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name.Trim(),
            Brand = Trim(request.Brand),
            Cnpj = request.Cnpj is null ? null : new string(request.Cnpj.Where(char.IsDigit).ToArray()),
            City = Trim(request.City),
            State = request.State?.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAt = utc,
            UpdatedAt = utc,
        }, cancellationToken).ConfigureAwait(false);
        return Map(entity);
    }

    public async Task<StationDto?> UpdateAsync(Guid id, Guid companyId, UpdateStationRequest request, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, companyId, cancellationToken).ConfigureAwait(false);
        if (s is null) return null;
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Nome do posto é obrigatório.");

        s.Name = request.Name.Trim();
        s.Brand = Trim(request.Brand);
        s.City = Trim(request.City);
        s.State = request.State?.Trim().ToUpperInvariant();
        s.UpdatedAt = DateTime.UtcNow;
        var ok = await _repository.UpdateAsync(s, cancellationToken).ConfigureAwait(false);
        return ok ? Map(s) : null;
    }

    public Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default) =>
        _repository.DeactivateAsync(id, companyId, cancellationToken);

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static StationDto Map(Station s) =>
        new(s.Id, s.CompanyId, s.Name, s.Brand, s.City, s.State, s.IsActive, s.CreatedAt);
}
