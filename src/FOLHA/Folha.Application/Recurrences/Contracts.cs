namespace Folha.Application.Recurrences;

public sealed record RecurrenceDto(
    Guid Id,
    Guid UserId,
    string Frequency,
    int EveryN,
    byte? DayOfMonth,
    byte? DayOfWeek,
    byte? MonthOfYear,
    DateTime StartDate,
    DateTime? EndDate,
    int? MaxOccurrences,
    DateTime? LastGeneratedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateRecurrenceRequest(
    string Frequency,
    int EveryN,
    byte? DayOfMonth,
    byte? DayOfWeek,
    byte? MonthOfYear,
    DateTime StartDate,
    DateTime? EndDate,
    int? MaxOccurrences);

public sealed record UpdateRecurrenceRequest(
    string Frequency,
    int EveryN,
    byte? DayOfMonth,
    byte? DayOfWeek,
    byte? MonthOfYear,
    DateTime StartDate,
    DateTime? EndDate,
    int? MaxOccurrences,
    DateTime? LastGeneratedAt);
