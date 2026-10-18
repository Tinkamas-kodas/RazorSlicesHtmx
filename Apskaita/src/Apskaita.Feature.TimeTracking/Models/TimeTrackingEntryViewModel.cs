using Apskaita.Shared;
using Apskaita.Shared.ProjectColors;

namespace Apskaita.Feature.TimeTracking.Models;

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
    int DescriptionLineClamp,
    bool IsCertified)
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
            descriptionLineClamp,
            row.InWorkAct);
    }
}