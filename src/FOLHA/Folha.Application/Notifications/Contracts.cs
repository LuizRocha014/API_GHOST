namespace Folha.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Kind,
    string Title,
    string? Body,
    string? RelatedKind,
    string? RelatedId,
    DateTime? ScheduledFor,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateNotificationRequest(
    string Kind,
    string Title,
    string? Body,
    string? RelatedKind = null,
    string? RelatedId = null,
    DateTime? ScheduledFor = null);

public sealed record UpdateNotificationRequest(
    string Kind,
    string Title,
    string? Body,
    string? RelatedKind,
    string? RelatedId,
    DateTime? ScheduledFor,
    bool IsRead);
