namespace Domain.Entities;

public sealed class AuditLog : Core
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
}
