namespace Domain.Entities;

public sealed class ProductImage : Core
{
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsMain { get; set; }
}
