namespace Abasta.Domain.Entities;

/// <summary>Foto do cupom fiscal e o resultado do OCR que preenche o abastecimento.</summary>
public sealed class Receipt
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? ByteSize { get; set; }
    public string OcrStatus { get; set; } = "pending";
    public decimal? OcrConfidence { get; set; }
    public string? OcrRawJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
