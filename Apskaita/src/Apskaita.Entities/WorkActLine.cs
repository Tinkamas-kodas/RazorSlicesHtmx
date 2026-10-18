namespace Apskaita.Entities;

/// <summary>
/// One line of a work act: a frozen copy of the certified work (duration, rate, start, description) that
/// keeps a reference to the <see cref="TimeTracking"/> entry it was made from. Editing the line never
/// changes the entry and editing the entry never changes existing lines; the same entry may appear in the
/// lines of several acts, which locks the entry against edit, delete and move.
/// </summary>
public class WorkActLine : BaseEntity
{
    public Guid Id { get; set; }
    public Guid WorkActId { get; set; }

    /// <summary>The entry this line was snapshotted from; the entry is locked while any line references it.</summary>
    public Guid TimeTrackingId { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary>Never null: the entry's own rate, else the project rate, or an explicit override at creation.</summary>
    public int HourlyRate { get; set; }

    /// <summary>Copy of the entry start (naive UTC) at snapshot time.</summary>
    public DateTime StartedAtUtc { get; set; }

    public string Description { get; set; } = string.Empty;
}
