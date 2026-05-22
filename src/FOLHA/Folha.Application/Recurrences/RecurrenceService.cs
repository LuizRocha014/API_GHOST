using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Recurrences;

public sealed class RecurrenceService : IRecurrenceService
{
    private static readonly HashSet<string> ValidFrequencies =
        new(StringComparer.OrdinalIgnoreCase) { "daily", "weekly", "monthly", "yearly" };

    private readonly IRecurrenceRepository _repository;

    public RecurrenceService(IRecurrenceRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<RecurrenceDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(r => r.ToDto()).ToList();
    }

    public async Task<RecurrenceDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var r = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return r?.ToDto();
    }

    public async Task<RecurrenceDto> CreateAsync(Guid userId, CreateRecurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var freq = (request.Frequency ?? "monthly").Trim().ToLowerInvariant();
        if (!ValidFrequencies.Contains(freq))
            throw new InvalidOperationException("Frequency deve ser daily, weekly, monthly ou yearly.");
        if (request.EveryN < 1)
            throw new InvalidOperationException("EveryN precisa ser >= 1.");

        var utc = DateTime.UtcNow;
        var entity = new Recurrence
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Frequency = freq,
            EveryN = request.EveryN,
            DayOfMonth = request.DayOfMonth,
            DayOfWeek = request.DayOfWeek,
            MonthOfYear = request.MonthOfYear,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate?.Date,
            MaxOccurrences = request.MaxOccurrences,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<RecurrenceDto?> UpdateAsync(Guid id, Guid userId, UpdateRecurrenceRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        var freq = (request.Frequency ?? "monthly").Trim().ToLowerInvariant();
        if (!ValidFrequencies.Contains(freq))
            throw new InvalidOperationException("Frequency inválido.");

        entity.Frequency = freq;
        entity.EveryN = request.EveryN;
        entity.DayOfMonth = request.DayOfMonth;
        entity.DayOfWeek = request.DayOfWeek;
        entity.MonthOfYear = request.MonthOfYear;
        entity.StartDate = request.StartDate.Date;
        entity.EndDate = request.EndDate?.Date;
        entity.MaxOccurrences = request.MaxOccurrences;
        entity.LastGeneratedAt = request.LastGeneratedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);
}
