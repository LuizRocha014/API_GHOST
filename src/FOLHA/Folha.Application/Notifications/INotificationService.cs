namespace Folha.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationDto> CreateAsync(Guid userId, CreateNotificationRequest request, CancellationToken cancellationToken = default);
    Task<NotificationDto?> UpdateAsync(Guid id, Guid userId, UpdateNotificationRequest request, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
