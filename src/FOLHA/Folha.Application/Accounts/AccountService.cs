using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Accounts;

public sealed class AccountService : IAccountService
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.OrdinalIgnoreCase) { "checking", "savings", "cash", "investment", "other" };

    private readonly IAccountRepository _repository;

    public AccountService(IAccountRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<AccountDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(a => a.ToDto()).ToList();
    }

    public async Task<AccountDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return item?.ToDto();
    }

    public async Task<AccountDto> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var kind = (request.Kind ?? "checking").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser checking, savings, cash, investment ou other.");

        var utc = DateTime.UtcNow;
        var entity = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Kind = kind,
            Institution = string.IsNullOrWhiteSpace(request.Institution) ? null : request.Institution.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex.Trim(),
            InitialBalance = request.InitialBalance,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "BRL" : request.CurrencyCode.Trim().ToUpperInvariant(),
            IsArchived = false,
            IncludeInTotal = request.IncludeInTotal,
            SortOrder = request.SortOrder,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<AccountDto?> UpdateAsync(Guid id, Guid userId, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        var kind = (request.Kind ?? "checking").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser checking, savings, cash, investment ou other.");

        entity.Name = request.Name.Trim();
        entity.Kind = kind;
        entity.Institution = string.IsNullOrWhiteSpace(request.Institution) ? null : request.Institution.Trim();
        entity.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        entity.ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex.Trim();
        entity.InitialBalance = request.InitialBalance;
        entity.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        entity.IsArchived = request.IsArchived;
        entity.IncludeInTotal = request.IncludeInTotal;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ArchiveAsync(id, userId, cancellationToken);
}
