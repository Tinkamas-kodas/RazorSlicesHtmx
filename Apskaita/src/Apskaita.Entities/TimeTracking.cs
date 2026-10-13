namespace Apskaita.Entities;

public class TimeTracking : BaseEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    /// <summary>Entry start as naive UTC value; the UI renders it in the browser's zone (tz-offset cookie).</summary>
    public DateTime StartedAtUtc { get; set; }
    public int DurationInMinutes { get; set; }
    public int? HourlyRate { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Calendar event this entry was created from; NULL for manual entries. Owned type in the same
    /// table, uniquely indexed — at most one entry per event (see <see cref="CalendarEventReference"/>).
    /// </summary>
    public CalendarEventReference? CalendarEvent { get; set; }
}
