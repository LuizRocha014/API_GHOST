namespace Application.ProductImages;

public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    string Url,
    bool IsMain,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt);
