namespace Folha.Application.Categories;

public sealed record CategoryDto(
    int Id,
    Guid? UserId,
    string Slug,
    string Label,
    string? Icon,
    string? ColorHex,
    string? BgHex,
    string Kind,
    bool IsSystem,
    bool IsArchived,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateCategoryRequest(
    string Slug,
    string Label,
    string? Icon,
    string? ColorHex,
    string? BgHex,
    string Kind,
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    string Slug,
    string Label,
    string? Icon,
    string? ColorHex,
    string? BgHex,
    string Kind,
    bool IsArchived,
    int SortOrder);
