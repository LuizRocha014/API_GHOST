namespace Folha.Application.Recurrences;

public interface IRecurrenceService
{
    Task<IReadOnlyList<RecurrenceDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RecurrenceDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<RecurrenceDto> CreateAsync(Guid userId, CreateRecurrenceRequest request, CancellationToken cancellationToken = default);
    Task<RecurrenceDto?> UpdateAsync(Guid id, Guid userId, UpdateRecurrenceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
