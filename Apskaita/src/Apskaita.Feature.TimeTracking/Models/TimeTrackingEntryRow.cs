namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// Join projection of a time entry with its project's colour — the shape the grid renders from.
/// <see cref="InWorkAct"/> is true when a work act line references the entry, which locks it.
/// </summary>
internal sealed record TimeTrackingEntryRow(
    Guid Id,
    DateTime StartedAtUtc,
    int DurationInMinutes,
    string Description,
    string ColorRgb,
    bool InWorkAct = false);