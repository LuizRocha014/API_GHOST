namespace Domain.Entities;

public sealed class Branch : Core
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
}
