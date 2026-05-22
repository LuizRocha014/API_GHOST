using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Transfers;

public sealed class TransferService : ITransferService
{
    private readonly ITransferRepository _repository;

    public TransferService(ITransferRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<TransferDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(t => t.ToDto()).ToList();
    }

    public async Task<TransferDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var t = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return t?.ToDto();
    }

    public async Task<TransferDto> CreateAsync(Guid userId, CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FromAccountId == request.ToAccountId)
            throw new InvalidOperationException("Conta de origem e destino não podem ser iguais.");
        if (request.Amount <= 0)
            throw new InvalidOperationException("Amount precisa ser positivo.");
        if (request.Fee < 0)
            throw new InvalidOperationException("Fee não pode ser negativo.");

        var utc = DateTime.UtcNow;
        var entity = new Transfer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            Amount = request.Amount,
            Fee = request.Fee,
            OccurredAt = request.OccurredAt,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<TransferDto?> UpdateAsync(Guid id, Guid userId, UpdateTransferRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        if (request.FromAccountId == request.ToAccountId)
            throw new InvalidOperationException("Conta de origem e destino não podem ser iguais.");
        if (request.Amount <= 0)
            throw new InvalidOperationException("Amount precisa ser positivo.");

        entity.FromAccountId = request.FromAccountId;
        entity.ToAccountId = request.ToAccountId;
        entity.Amount = request.Amount;
        entity.Fee = request.Fee;
        entity.OccurredAt = request.OccurredAt;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);
}
