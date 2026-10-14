using System.Globalization;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using Apskaita.Shared.ProjectColors;

namespace Apskaita.Feature.TimeTracking.Models;

public sealed record TimeTrackingPageModel(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    DateOnly PreviousWeekStart,
    DateOnly NextWeekStart,
    IReadOnlyList<TimeTrackingDayModel> Days,
    IReadOnlyList<TimeTrackingSlotModel> Slots,
    IReadOnlyList<TimeTrackingEntryViewModel> Entries,
    IReadOnlyList<TimeTrackingCalendarEventViewModel> CalendarEvents,
    bool HasCalendarConnector,
    string? CalendarNotice);

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

/// <summary>
/// One half-hour row of the week grid.
/// </summary>
/// <param name="Index">Slot index from the start of the day (0..47).</param>
/// <param name="Start">Slot start (display-zone wall time).</param>
/// <param name="End">Slot end (display-zone wall time).</param>
/// <param name="IsWorkHours">True when the slot lies in the highlighted work band.</param>
public sealed record TimeTrackingSlotModel(
    int Index,
    TimeOnly Start,
    TimeOnly End,
    bool IsWorkHours)
{
    /// <summary>
    /// The full day of slots (<see cref="TimeTrackingGridGeometry.SlotsPerDay"/> ×
    /// <see cref="TimeTrackingGridGeometry.SlotMinutes"/> minutes), flagged for the work band.
    /// </summary>
    internal static TimeTrackingSlotModel[] WorkSlots() =>
        Enumerable
            .Range(0, TimeTrackingGridGeometry.SlotsPerDay)
            .Select(index =>
            {
                var start = TimeOnly.MinValue.AddMinutes(index * TimeTrackingGridGeometry.SlotMinutes);
                var end = start.AddMinutes(TimeTrackingGridGeometry.SlotMinutes);
                var isWorkHours = start >= TimeTrackingGridGeometry.WorkDayStart && start < TimeTrackingGridGeometry.WorkDayEnd;
                return new TimeTrackingSlotModel(index, start, end, isWorkHours);
            })
            .ToArray();
}

/// <summary>
/// Join projection of a time entry with its project's colour — the shape the grid renders from.
/// </summary>
internal sealed record TimeTrackingEntryRow(
    Guid Id,
    DateTime StartedAtUtc,
    int DurationInMinutes,
    string Description,
    string ColorRgb);

/// <summary>
/// An entry card positioned inside a day column. All times are display-zone wall times; the geometry
/// is expressed in slot units and pixels.
/// </summary>
public sealed record TimeTrackingEntryViewModel(
    Guid Id,
    DateOnly StartDate,
    int StartSlotIndex,
    TimeOnly Start,
    TimeOnly DisplayEnd,
    string Description,
    string ProjectColorRgb,
    string AccentColorRgb,
    string TextColorRgb,
    double OffsetTopPx,
    double HeightPx,
    decimal OverflowHours,
    int DescriptionLineClamp)
{
    /// <summary>
    /// Builds the card for <paramref name="row"/>. Stored UTC is converted to display-zone wall time;
    /// an entry crossing midnight is clipped at the day boundary and its remainder reported as
    /// <paramref name="displayOffset"/>-independent <c>OverflowHours</c>. Colours come from
    /// <see cref="ProjectColorCatalog"/> so text stays readable on any project colour.
    /// </summary>
    internal static TimeTrackingEntryViewModel From(TimeTrackingEntryRow row, TimeSpan displayOffset)
    {
        // Stored UTC → display-zone wall time; all grid math below works in wall time.
        var startedAtLocal = BrowserTimeZone.UtcToWall(row.StartedAtUtc, displayOffset);
        var durationMinutes = Math.Clamp(row.DurationInMinutes, TimeTrackingLimits.MinDurationMinutes, TimeTrackingLimits.MaxDurationMinutes);
        var entryEnd = startedAtLocal.AddMinutes(durationMinutes);
        var dayEnd = startedAtLocal.Date.AddDays(1);
        var visibleEnd = entryEnd <= dayEnd ? entryEnd : dayEnd;

        var visibleMinutes = Math.Max(1, (int)Math.Ceiling((visibleEnd - startedAtLocal).TotalMinutes));
        var overflowMinutes = Math.Max(0, (int)Math.Ceiling((entryEnd - dayEnd).TotalMinutes));
        var overflowHours = overflowMinutes <= 0
            ? 0m
            : (decimal)Math.Ceiling(overflowMinutes / 15d) / 4m;

        var minutesFromDayStart = (int)(startedAtLocal - startedAtLocal.Date).TotalMinutes;
        var startSlotIndex = Math.Clamp(minutesFromDayStart / TimeTrackingGridGeometry.SlotMinutes, 0, TimeTrackingGridGeometry.SlotsPerDay - 1);
        var offsetInSlotMinutes = minutesFromDayStart % TimeTrackingGridGeometry.SlotMinutes;
        var offsetTopPx = offsetInSlotMinutes * (TimeTrackingGridGeometry.SlotHeightPx / TimeTrackingGridGeometry.SlotMinutes);
        var heightPx = visibleMinutes * (TimeTrackingGridGeometry.SlotHeightPx / TimeTrackingGridGeometry.SlotMinutes);
        var effectiveHeightPx = Math.Max(2d, heightPx);
        var descriptionLineClamp = TimeTrackingGridGeometry.DescriptionLineClamp(effectiveHeightPx);
        var colorPresentation = ProjectColorCatalog.CreatePresentation(row.ColorRgb);

        return new TimeTrackingEntryViewModel(
            row.Id,
            DateOnly.FromDateTime(startedAtLocal),
            startSlotIndex,
            TimeOnly.FromDateTime(startedAtLocal),
            TimeOnly.FromDateTime(entryEnd),
            string.IsNullOrWhiteSpace(row.Description) ? "No description" : row.Description.Trim(),
            colorPresentation.BackgroundColorRgb,
            colorPresentation.AccentColorRgb,
            colorPresentation.TextColorRgb,
            offsetTopPx,
            effectiveHeightPx,
            overflowHours,
            descriptionLineClamp);
    }
}

/// <summary>
/// A calendar event rendered in the time tracking side panel. Times are display-zone wall times;
/// <see cref="DayDate"/> groups the event under its day header. The identity triple
/// (<see cref="ProviderKey"/>, <see cref="AccountKey"/>, <see cref="EventId"/>) builds the single
/// <c>event</c> URL; <see cref="IsTracked"/> renders the "Logged" badge while the server still
/// decides create-vs-edit on click.
/// </summary>
public sealed record TimeTrackingCalendarEventViewModel(
    DateOnly DayDate,
    string Subject,
    TimeOnly Start,
    TimeOnly End,
    string? Location,
    bool IsOnline,
    string? OrganizerName,
    string ProviderKey,
    string AccountKey,
    string EventId,
    int DurationMinutes,
    bool IsTracked)
{
    /// <summary>
    /// Maps a reader event to its panel row; <paramref name="isTracked"/> comes from the set of identity
    /// triples already logged in the displayed week. Duration is rounded to whole minutes, minimum 1.
    /// </summary>
    internal static TimeTrackingCalendarEventViewModel From(CalendarEventInfo e, bool isTracked) => new(
        DateOnly.FromDateTime(e.StartLocal),
        e.Subject,
        TimeOnly.FromDateTime(e.StartLocal),
        TimeOnly.FromDateTime(e.EndLocal),
        e.Location,
        e.IsOnline,
        e.OrganizerName,
        e.ProviderKey,
        e.AccountKey,
        e.EventId,
        Math.Max(1, (int)Math.Round((e.EndLocal - e.StartLocal).TotalMinutes)),
        isTracked);
}
