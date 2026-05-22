namespace Application.ProductBatches;

/// <summary>
/// Ajuste de validade e flag do lote. Quantidades e custos só mudam via movimentações de estoque.
/// </summary>
public sealed record UpdateProductBatchMetadataRequest(DateTime? ExpirationDate, bool Active);
