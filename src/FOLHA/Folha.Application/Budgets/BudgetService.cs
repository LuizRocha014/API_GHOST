using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Budgets;

public sealed class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _repository;

    public BudgetService(IBudgetRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<BudgetDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(b => b.ToDto()).ToList();
    }

    public async Task<BudgetDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var b = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return b?.ToDto();
    }

    public async Task<BudgetDto> CreateAsync(Guid userId, CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AmountLimit <= 0)
            throw new InvalidOperationException("AmountLimit precisa ser positivo.");
        if (request.AlertThreshold > 100)
            throw new InvalidOperationException("AlertThreshold deve estar entre 0 e 100.");

        var utc = DateTime.UtcNow;
        var entity = new Budget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = request.CategoryId,
            ReferenceMonth = new DateTime(request.ReferenceMonth.Year, request.ReferenceMonth.Month, 1),
            AmountLimit = request.AmountLimit,
            Rollover = request.Rollover,
            AlertThreshold = request.AlertThreshold,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<BudgetDto?> UpdateAsync(Guid id, Guid userId, UpdateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        if (request.AmountLimit <= 0)
            throw new InvalidOperationException("AmountLimit precisa ser positivo.");

        entity.CategoryId = request.CategoryId;
        entity.ReferenceMonth = new DateTime(request.ReferenceMonth.Year, request.ReferenceMonth.Month, 1);
        entity.AmountLimit = request.AmountLimit;
        entity.Rollover = request.Rollover;
        entity.AlertThreshold = request.AlertThreshold;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);
}
