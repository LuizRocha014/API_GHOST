namespace Domain.Entities;

/// <summary>
/// Base para entidades persistíveis: identificador GUID, auditoria e soft-delete.
/// </summary>
public abstract class Core
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Active { get; set; } = true;
}
