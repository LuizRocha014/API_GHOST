using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;

namespace Abasta.Application.FuelAccounts;

public sealed record FuelAccountDto(
    Guid Id,
    Guid CompanyId,
    String Distributor,
    String? AccountNumber,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateFuelAccountRequest(
    String Distributor,
    String? AccountNumber = null,
    Guid? Id = null);

public sealed record UpdateFuelAccountRequest(String Distributor, String? AccountNumber);

public interface IFuelAccountService
{
    Task<IReadOnlyList<FuelAccountDto>> ListAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<FuelAccountDto> CreateAsync(Guid companyId, CreateFuelAccountRequest request, CancellationToken cancellationToken = default);
    Task<FuelAccountDto?> UpdateAsync(Guid id, Guid companyId, UpdateFuelAccountRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class FuelAccountService : IFuelAccountService
{
    private readonly IFuelAccountRepository _repository;

    public FuelAccountService(IFuelAccountRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<FuelAccountDto>> ListAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByCompanyAsync(companyId, cancellationToken).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<FuelAccountDto> CreateAsync(Guid companyId, CreateFuelAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Distributor))
            throw new InvalidOperationException("Distribuidora é obrigatória.");

        var utc = DateTime.UtcNow;
        var entity = await _repository.AddAsync(new FuelAccount
        {
            Id = request.Id ?? Guid.NewGuid(),
            CompanyId = companyId,
            Distributor = request.Distributor.Trim(),
            AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber) ? null : request.AccountNumber.Trim(),
            IsActive = true,
            CreatedAt = utc,
            UpdatedAt = utc,
        }, cancellationToken).ConfigureAwait(false);
        return Map(entity);
    }

    public async Task<FuelAccountDto?> UpdateAsync(Guid id, Guid companyId, UpdateFuelAccountRequest request, CancellationToken cancellationToken = default)
    {
        var a = await _repository.GetByIdAsync(id, companyId, cancellationToken).ConfigureAwait(false);
        if (a is null) return null;
        if (string.IsNullOrWhiteSpace(request.Distributor))
            throw new InvalidOperationException("Distribuidora é obrigatória.");

        a.Distributor = request.Distributor.Trim();
        a.AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber) ? null : request.AccountNumber.Trim();
        a.UpdatedAt = DateTime.UtcNow;
        var ok = await _repository.UpdateAsync(a, cancellationToken).ConfigureAwait(false);
        return ok ? Map(a) : null;
    }

    public Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default) =>
        _repository.DeactivateAsync(id, companyId, cancellationToken);

    private static FuelAccountDto Map(FuelAccount a) =>
        new(a.Id, a.CompanyId, a.Distributor, a.AccountNumber, a.IsActive, a.CreatedAt);
}
