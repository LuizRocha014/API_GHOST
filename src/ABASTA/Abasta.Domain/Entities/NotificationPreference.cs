namespace Abasta.Domain.Entities;

/// <summary>Preferências de notificação (1:1 com o usuário). PK = user_id.</summary>
public sealed class NotificationPreference
{
    public Guid UserId { get; set; }
    public bool NewReceiptEnabled { get; set; } = true;
    public bool WeeklyReportEnabled { get; set; } = true;
    public bool OverBudgetEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
