using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.GoalContributions;

public sealed class GoalContributionService : IGoalContributionService
{
    private readonly IGoalContributionRepository _repository;

    public GoalContributionService(IGoalContributionRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<GoalContributionDto>> ListByGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByGoalAsync(goalId, cancellationToken).ConfigureAwait(false);
        return items.Select(g => g.ToDto()).ToList();
    }

    public async Task<GoalContributionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gc = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return gc?.ToDto();
    }

    public async Task<GoalContributionDto> CreateAsync(CreateGoalContributionRequest request, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        var entity = new GoalContribution
        {
            Id = Guid.NewGuid(),
            GoalId = request.GoalId,
            TransactionId = request.TransactionId,
            Amount = request.Amount,
            ContributedAt = request.ContributedAt,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<GoalContributionDto?> UpdateAsync(Guid id, UpdateGoalContributionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        entity.TransactionId = request.TransactionId;
        entity.Amount = request.Amount;
        entity.ContributedAt = request.ContributedAt;
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);
}
