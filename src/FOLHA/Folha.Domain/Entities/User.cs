namespace Folha.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public string Locale { get; set; } = "pt-BR";
    public string CurrencyCode { get; set; } = "BRL";
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
