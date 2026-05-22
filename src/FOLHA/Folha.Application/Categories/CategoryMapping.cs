using Folha.Domain.Entities;

namespace Folha.Application.Categories;

internal static class CategoryMapping
{
    public static CategoryDto ToDto(this Category c) => new(
        c.Id, c.UserId, c.Slug, c.Label, c.Icon, c.ColorHex, c.BgHex,
        c.Kind, c.IsSystem, c.IsArchived, c.SortOrder, c.CreatedAt, c.UpdatedAt);
}
