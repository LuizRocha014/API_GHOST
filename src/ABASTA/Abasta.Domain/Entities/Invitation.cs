namespace Abasta.Domain.Entities;

/// <summary>Convite de um gestor para um colaborador entrar na empresa.</summary>
public sealed class Invitation
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "collaborator";
    public Guid InvitedByUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsPending => Status == "pending" && ExpiresAt > DateTime.UtcNow;
}
