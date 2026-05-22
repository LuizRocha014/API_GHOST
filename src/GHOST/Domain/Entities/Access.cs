namespace Domain.Entities;

public sealed class Access : Core
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}
