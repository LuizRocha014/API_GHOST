using Domain.Entities;

namespace Application.Accesses;

internal static class AccessMapping
{
    internal static AccessDto ToDto(this Access e) =>
        new(e.Id, e.Name, e.Code, e.Active, e.CreatedAt, e.UpdatedAt);
}
