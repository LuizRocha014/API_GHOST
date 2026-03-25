namespace Application.Accesses;

public sealed record AccessDto(Guid Id, string Name, string? Code, bool Active, DateTime CreatedAt, DateTime UpdatedAt);
