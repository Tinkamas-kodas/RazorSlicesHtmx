namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// Join projection of a time entry with its project's colour — the shape the grid renders from.
/// </summary>
internal sealed record TimeTrackingEntryRow(
    Guid Id,
    DateTime StartedAtUtc,
    int DurationInMinutes,
    string Description,
    string ColorRgb);