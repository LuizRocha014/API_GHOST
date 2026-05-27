namespace Abasta.Domain.Entities;

/// <summary>Alerta do painel do gestor (estourou limite, nota sem KM, tendência da frota…).</summary>
public sealed class Alert
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Severity { get; set; } = "info";
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public Guid? TargetUserId { get; set; }
    public Guid? TargetVehicleId { get; set; }
    public DateTime? ReferenceMonth { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
