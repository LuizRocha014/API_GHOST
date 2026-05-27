namespace Abasta.Domain.Entities;

/// <summary>Tipo de combustível (lookup): Gasolina, Etanol, Diesel S-10, GNV.</summary>
public sealed class FuelType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
