namespace Folha.Domain.Entities;

public sealed class Recurrence
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Frequency { get; set; } = "monthly";
    public int EveryN { get; set; } = 1;
    public byte? DayOfMonth { get; set; }
    public byte? DayOfWeek { get; set; }
    public byte? MonthOfYear { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxOccurrences { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
