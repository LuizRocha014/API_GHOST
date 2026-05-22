namespace Folha.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "system";
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? RelatedKind { get; set; }
    public string? RelatedId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
