using System.Globalization;

namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// One day column of the week grid.
/// </summary>
/// <param name="Date">The calendar date in the display zone.</param>
/// <param name="Label">Header label, e.g. <c>Mon 05.10</c> (invariant formatting).</param>
/// <param name="IsToday">True for the display-zone today (header highlight).</param>
public sealed record TimeTrackingDayModel(
    DateOnly Date,
    string Label,
    bool IsToday)
{
    /// <summary>
    /// The seven day columns of the week starting at <paramref name="weekStart"/> (Monday), each labelled
    /// <c>ddd dd.MM</c> in invariant culture; <paramref name="todayDisplay"/> marks the highlighted column.
    /// </summary>
    internal static TimeTrackingDayModel[] WeekOf(DateOnly weekStart, DateOnly todayDisplay) =>
        Enumerable
            .Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                return new TimeTrackingDayModel(
                    day,
                    day.ToString("ddd dd.MM", CultureInfo.InvariantCulture),
                    day == todayDisplay);
            })
            .ToArray();
}