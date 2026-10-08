namespace Apskaita.Entities;

public class TimeTracking : BaseEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime StartedAtLocal { get; set; }
    public int DurationInMinutes { get; set; }
    public int? HourlyRate { get; set; }
    public string Description { get; set; } = string.Empty;
}
