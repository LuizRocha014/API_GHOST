namespace Domain.Entities;

public sealed class RefreshToken : Core
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
