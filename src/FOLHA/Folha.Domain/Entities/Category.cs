namespace Folha.Domain.Entities;

public sealed class Category
{
    public int Id { get; set; }
    public Guid? UserId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ColorHex { get; set; }
    public string? BgHex { get; set; }
    public string Kind { get; set; } = "expense";
    public bool IsSystem { get; set; }
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
