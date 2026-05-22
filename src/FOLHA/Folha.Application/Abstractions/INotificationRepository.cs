using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
