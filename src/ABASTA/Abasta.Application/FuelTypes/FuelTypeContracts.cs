namespace Abasta.Application.FuelTypes;

public sealed record FuelTypeDto(
    int Id,
    string Code,
    string Label,
    string? ColorHex,
    int SortOrder);
