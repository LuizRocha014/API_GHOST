namespace Domain.Entities;

public sealed class RolePermission : Core
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}
