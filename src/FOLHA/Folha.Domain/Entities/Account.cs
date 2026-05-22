namespace Folha.Domain.Entities;

public sealed class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "checking";
    public string? Institution { get; set; }
    public string? Icon { get; set; }
    public string? ColorHex { get; set; }
    public decimal InitialBalance { get; set; }
    public string CurrencyCode { get; set; } = "BRL";
    public bool IsArchived { get; set; }
    public bool IncludeInTotal { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
