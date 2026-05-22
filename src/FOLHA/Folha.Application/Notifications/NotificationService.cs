using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;

    public NotificationService(INotificationRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(n => n.ToDto()).ToList();
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var n = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return n?.ToDto();
    }

    public async Task<NotificationDto> CreateAsync(Guid userId, CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = (request.Kind ?? "system").Trim().ToLowerInvariant(),
            Title = request.Title.Trim(),
            Body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim(),
            RelatedKind = string.IsNullOrWhiteSpace(request.RelatedKind) ? null : request.RelatedKind.Trim(),
            RelatedId = string.IsNullOrWhiteSpace(request.RelatedId) ? null : request.RelatedId.Trim(),
            ScheduledFor = request.ScheduledFor,
            IsRead = false,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<NotificationDto?> UpdateAsync(Guid id, Guid userId, UpdateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        entity.Kind = (request.Kind ?? "system").Trim().ToLowerInvariant();
        entity.Title = request.Title.Trim();
        entity.Body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim();
        entity.RelatedKind = string.IsNullOrWhiteSpace(request.RelatedKind) ? null : request.RelatedKind.Trim();
        entity.RelatedId = string.IsNullOrWhiteSpace(request.RelatedId) ? null : request.RelatedId.Trim();
        entity.ScheduledFor = request.ScheduledFor;
        if (request.IsRead && !entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;
        }
        else if (!request.IsRead && entity.IsRead)
        {
            entity.IsRead = false;
            entity.ReadAt = null;
        }
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.MarkAsReadAsync(id, userId, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, userId, cancellationToken);
}
