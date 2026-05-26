namespace Folha.Domain.Entities;

public sealed class CreditCard
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = "other";
    public string? LastFour { get; set; }
    public decimal CreditLimit { get; set; }
    public byte ClosingDay { get; set; }
    public byte DueDay { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
