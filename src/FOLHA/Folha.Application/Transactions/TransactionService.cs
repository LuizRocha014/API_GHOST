using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Transactions;

public sealed class TransactionService : ITransactionService
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.OrdinalIgnoreCase) { "expense", "income", "transfer_out", "transfer_in" };

    private readonly ITransactionRepository _repository;

    public TransactionService(ITransactionRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<TransactionDto>> ListAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (take <= 0 || take > 200) take = 50;
        var items = await _repository.GetAllByUserAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(t => t.ToDto()).ToList();
    }

    public async Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var t = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return t?.ToDto();
    }

    public async Task<TransactionDto> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Kind, request.Amount, request.AccountId, request.CreditCardId, request.InstallmentNumber, request.InstallmentTotal);

        var utc = DateTime.UtcNow;
        var entity = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = request.AccountId,
            CreditCardId = request.CreditCardId,
            CreditCardStatementId = request.CreditCardStatementId,
            CategoryId = request.CategoryId,
            Description = request.Description.Trim(),
            Place = string.IsNullOrWhiteSpace(request.Place) ? null : request.Place.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Amount = request.Amount,
            Kind = request.Kind.Trim().ToLowerInvariant(),
            OccurredAt = request.OccurredAt,
            InstallmentNumber = request.InstallmentNumber,
            InstallmentTotal = request.InstallmentTotal,
            IsPending = request.IsPending,
            IsExcludedFromReports = false,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<TransactionDto?> UpdateAsync(Guid id, Guid userId, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        Validate(request.Kind, request.Amount, request.AccountId, request.CreditCardId, request.InstallmentNumber, request.InstallmentTotal);

        entity.AccountId = request.AccountId;
        entity.CreditCardId = request.CreditCardId;
        entity.CreditCardStatementId = request.CreditCardStatementId;
        entity.CategoryId = request.CategoryId;
        entity.Description = request.Description.Trim();
        entity.Place = string.IsNullOrWhiteSpace(request.Place) ? null : request.Place.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.Amount = request.Amount;
        entity.Kind = request.Kind.Trim().ToLowerInvariant();
        entity.OccurredAt = request.OccurredAt;
        entity.InstallmentNumber = request.InstallmentNumber;
        entity.InstallmentTotal = request.InstallmentTotal;
        entity.IsPending = request.IsPending;
        entity.IsExcludedFromReports = request.IsExcludedFromReports;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.SoftDeleteAsync(id, userId, cancellationToken);

    private static void Validate(string kind, decimal amount, Guid? accountId, Guid? creditCardId, short? installNumber, short? installTotal)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Amount precisa ser positivo.");
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind inválido.");
        if (accountId is null && creditCardId is null)
            throw new InvalidOperationException("Informe AccountId ou CreditCardId.");
        if (installNumber.HasValue && installTotal.HasValue && (installNumber < 1 || installNumber > installTotal))
            throw new InvalidOperationException("Parcela inválida.");
    }
}
