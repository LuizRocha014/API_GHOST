using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Bills;

public sealed class BillService : IBillService
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.OrdinalIgnoreCase) { "payable", "receivable" };

    private static readonly HashSet<string> ValidStatus =
        new(StringComparer.OrdinalIgnoreCase) { "pending", "paid", "received", "overdue", "cancelled" };

    private readonly IBillRepository _repository;

    public BillService(IBillRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<BillDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(b => b.ToDto()).ToList();
    }

    public async Task<BillDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var b = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return b?.ToDto();
    }

    public async Task<BillDto> CreateAsync(Guid userId, CreateBillRequest request, CancellationToken cancellationToken = default)
    {
        var kind = (request.Kind ?? "payable").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser payable ou receivable.");
        if (request.Amount <= 0)
            throw new InvalidOperationException("Amount deve ser positivo.");

        var utc = DateTime.UtcNow;
        var entity = new Bill
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            RecurrenceId = request.RecurrenceId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Kind = kind,
            DueDate = request.DueDate.Date,
            Status = "pending",
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<BillDto?> UpdateAsync(Guid id, Guid userId, UpdateBillRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        var kind = (request.Kind ?? "payable").Trim().ToLowerInvariant();
        var status = (request.Status ?? "pending").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser payable ou receivable.");
        if (!ValidStatus.Contains(status))
            throw new InvalidOperationException("Status inválido.");

        entity.AccountId = request.AccountId;
        entity.CategoryId = request.CategoryId;
        entity.RecurrenceId = request.RecurrenceId;
        entity.Description = request.Description.Trim();
        entity.Amount = request.Amount;
        entity.Kind = kind;
        entity.DueDate = request.DueDate.Date;
        entity.Status = status;
        entity.PaidAt = request.PaidAt;
        entity.PaidTransactionId = request.PaidTransactionId;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);
}
