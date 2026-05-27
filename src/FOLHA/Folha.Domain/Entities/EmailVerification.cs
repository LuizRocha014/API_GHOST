namespace Folha.Domain.Entities;

/// <summary>
/// Código de verificação de e-mail (OTP) emitido durante o cadastro.
/// O código em si nunca é persistido — guardamos apenas o hash.
/// </summary>
public sealed class EmailVerification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
}
