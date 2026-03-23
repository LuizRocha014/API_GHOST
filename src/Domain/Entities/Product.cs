namespace Domain.Entities;

public sealed class Product
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
