namespace Abasta.Application.Analytics;

// As propriedades viram camelCase no JSON e batem com os modelos do app Flutter.

public sealed record TeamMemberDto(
    string Id, string Name, string Role, string Vehicle, string Email,
    decimal Total, decimal Liters, int Entries, int Delta);

public sealed record AlertDto(string Severity, string Title, string Subtitle);

public sealed record FuelBreakdownDto(string Fuel, decimal Value);

public sealed record TeamOverviewDto(
    string MonthLabel,
    string AccountName,
    int ActiveCount,
    decimal TotalSpent,
    decimal TotalLiters,
    int TotalEntries,
    decimal DeltaReaisVsLastMonth,
    IReadOnlyList<TeamMemberDto> Members,
    IReadOnlyList<AlertDto> Alerts,
    IReadOnlyList<FuelBreakdownDto> FuelBreakdown);

public sealed record MonthPointDto(string Month, decimal Value);
public sealed record FuelSliceDto(string Fuel, decimal Value, int Pct);
public sealed record StationStatDto(string Name, int Visits, decimal Value);

public sealed record ReportDto(
    bool IsAdmin,
    decimal LastMonthValue,
    decimal Total6Months,
    decimal MonthlyAvg,
    int ScopeCount,
    IReadOnlyList<MonthPointDto> Monthly,
    IReadOnlyList<FuelSliceDto> FuelSlices,
    IReadOnlyList<StationStatDto> TopStations);

public sealed record EntryDto(
    string Id, string UserId, string UserName, string Station, string Fuel,
    decimal Liters, decimal PricePerLiter, decimal Total, int Km,
    string When, string Date);

public sealed record DashboardDto(
    string MonthLabel,
    decimal Spent,
    decimal Budget,
    decimal DeltaReaisVsLastMonth,
    decimal Liters,
    decimal AvgConsumption,
    int EntriesCount,
    int DaysToMonthEnd,
    IReadOnlyList<EntryDto> RecentEntries);
