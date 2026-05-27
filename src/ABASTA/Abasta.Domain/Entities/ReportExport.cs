namespace Abasta.Domain.Entities;

/// <summary>Pedido de geração de relatório (PDF/CSV) — processado de forma assíncrona.</summary>
public sealed class ReportExport
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Scope { get; set; } = "company";
    public Guid? TargetUserId { get; set; }
    public Guid? TargetVehicleId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Format { get; set; } = "pdf";
    public string Status { get; set; } = "queued";
    public string? FileUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
