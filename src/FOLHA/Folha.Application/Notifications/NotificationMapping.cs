using Folha.Domain.Entities;

namespace Folha.Application.Notifications;

internal static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification n) => new(
        n.Id, n.UserId, n.Kind, n.Title, n.Body, n.RelatedKind, n.RelatedId,
        n.ScheduledFor, n.IsRead, n.ReadAt, n.CreatedAt, n.UpdatedAt);
}
