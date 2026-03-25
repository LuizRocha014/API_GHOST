namespace Application.Accesses;

public sealed record CreateAccessRequest(string Name, string? Code = null);
