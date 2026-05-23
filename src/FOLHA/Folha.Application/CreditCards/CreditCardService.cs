using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.CreditCards;

public sealed class CreditCardService : ICreditCardService
{
    private static readonly HashSet<string> ValidBrands =
        new(StringComparer.OrdinalIgnoreCase) { "visa", "master", "amex", "elo", "hiper", "other" };

    private readonly ICreditCardRepository _repository;

    public CreditCardService(ICreditCardRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CreditCardDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(c => c.ToDto()).ToList();
    }

    public async Task<CreditCardDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var c = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return c?.ToDto();
    }

    public async Task<CreditCardDto> CreateAsync(Guid userId, CreateCreditCardRequest request, CancellationToken cancellationToken = default)
    {
        var brand = (request.Brand ?? "other").Trim().ToLowerInvariant();
        if (!ValidBrands.Contains(brand))
            throw new InvalidOperationException("Brand inválido.");
        if (request.ClosingDay < 1 || request.ClosingDay > 31)
            throw new InvalidOperationException("ClosingDay deve estar entre 1 e 31.");
        if (request.DueDay < 1 || request.DueDay > 31)
            throw new InvalidOperationException("DueDay deve estar entre 1 e 31.");

        var utc = DateTime.UtcNow;
        var entity = new CreditCard
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserId = userId,
            AccountId = request.AccountId,
            Name = request.Name.Trim(),
            Brand = brand,
            LastFour = string.IsNullOrWhiteSpace(request.LastFour) ? null : request.LastFour.Trim(),
            CreditLimit = request.CreditLimit,
            ClosingDay = request.ClosingDay,
            DueDay = request.DueDay,
            IsArchived = false,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<CreditCardDto?> UpdateAsync(Guid id, Guid userId, UpdateCreditCardRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        var brand = (request.Brand ?? "other").Trim().ToLowerInvariant();
        if (!ValidBrands.Contains(brand))
            throw new InvalidOperationException("Brand inválido.");

        entity.AccountId = request.AccountId;
        entity.Name = request.Name.Trim();
        entity.Brand = brand;
        entity.LastFour = string.IsNullOrWhiteSpace(request.LastFour) ? null : request.LastFour.Trim();
        entity.CreditLimit = request.CreditLimit;
        entity.ClosingDay = request.ClosingDay;
        entity.DueDay = request.DueDay;
        entity.IsArchived = request.IsArchived;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ArchiveAsync(id, userId, cancellationToken);
}
