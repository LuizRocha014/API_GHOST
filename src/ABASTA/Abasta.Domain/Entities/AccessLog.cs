namespace Abasta.Domain.Entities;

/// <summary>Registro de auditoria de acesso (login, logout, refresh, etc.).</summary>
public sealed class AccessLog
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string Event { get; set; } = string.Empty;
    public bool Succeeded { get; set; } = true;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}
