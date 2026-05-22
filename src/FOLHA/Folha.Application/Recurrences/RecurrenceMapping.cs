using Folha.Domain.Entities;

namespace Folha.Application.Recurrences;

internal static class RecurrenceMapping
{
    public static RecurrenceDto ToDto(this Recurrence r) => new(
        r.Id, r.UserId, r.Frequency, r.EveryN, r.DayOfMonth, r.DayOfWeek,
        r.MonthOfYear, r.StartDate, r.EndDate, r.MaxOccurrences,
        r.LastGeneratedAt, r.CreatedAt, r.UpdatedAt);
}
