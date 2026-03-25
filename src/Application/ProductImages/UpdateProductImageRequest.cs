namespace Application.ProductImages;

public sealed record UpdateProductImageRequest(string Url, bool IsMain, bool Active);
