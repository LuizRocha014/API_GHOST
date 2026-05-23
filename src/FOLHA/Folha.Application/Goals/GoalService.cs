using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Goals;

public sealed class GoalService : IGoalService
{
    private readonly IGoalRepository _repository;

    public GoalService(IGoalRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<GoalDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(g => g.ToDto()).ToList();
    }

    public async Task<GoalDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var g = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return g?.ToDto();
    }

    public async Task<GoalDto> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TargetAmount <= 0)
            throw new InvalidOperationException("TargetAmount precisa ser positivo.");

        var utc = DateTime.UtcNow;
        var entity = new Goal
        {
            Id = request.Id ?? Guid.NewGuid(),
            UserId = userId,
            AccountId = request.AccountId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TargetAmount = request.TargetAmount,
            CurrentAmount = 0,
            TargetDate = request.TargetDate?.Date,
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex.Trim(),
            IsCompleted = false,
            IsArchived = false,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<GoalDto?> UpdateAsync(Guid id, Guid userId, UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        if (request.TargetAmount <= 0)
            throw new InvalidOperationException("TargetAmount precisa ser positivo.");

        entity.AccountId = request.AccountId;
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.TargetAmount = request.TargetAmount;
        entity.CurrentAmount = request.CurrentAmount;
        entity.TargetDate = request.TargetDate?.Date;
        entity.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        entity.ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex.Trim();
        entity.IsArchived = request.IsArchived;
        if (request.IsCompleted && !entity.IsCompleted)
        {
            entity.IsCompleted = true;
            entity.CompletedAt = DateTime.UtcNow;
        }
        else if (!request.IsCompleted && entity.IsCompleted)
        {
            entity.IsCompleted = false;
            entity.CompletedAt = null;
        }
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ArchiveAsync(id, userId, cancellationToken);
}
