using Apskaita.Shared.Calendar;

namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// A calendar event rendered inline in the time tracking grid as a grey card behind the entry cards.
/// Times are display-zone wall times. A timed event reuses the entry geometry (<see cref="StartSlotIndex"/>,
/// <see cref="OffsetTopPx"/>, <see cref="HeightPx"/>) so it lines up on the same day/time layout; an
/// all-day or multi-day event instead renders as a band in the dedicated all-day row, spanning
/// <see cref="AllDayFirstDate"/>..<see cref="AllDayLastDate"/>. The identity triple
/// (<see cref="ProviderKey"/>, <see cref="AccountKey"/>, <see cref="EventId"/>) builds the single
/// <c>event</c> URL; the server decides create-vs-toast on click. Events referenced by a same-week entry
/// are filtered out before this model is built, so a logged event never appears here.
/// </summary>
public sealed record TimeTrackingCalendarEventViewModel(
    DateOnly StartDate,
    int StartSlotIndex,
    TimeOnly Start,
    TimeOnly DisplayEnd,
    double OffsetTopPx,
    double HeightPx,
    decimal OverflowHours,
    string Subject,
    string ProviderKey,
    string AccountKey,
    string EventId,
    int DurationMinutes,
    bool IsAllDay,
    DateOnly AllDayFirstDate,
    DateOnly AllDayLastDate)
{
    /// <summary>
    /// Maps a reader event to its in-grid card. Duration is rounded to whole minutes (minimum 1). An event
    /// that starts and ends at midnight and spans at least a whole day is treated as all-day/multi-day and
    /// placed in the all-day row; a timed event is clipped at the day boundary with the remainder reported
    /// as <see cref="OverflowHours"/>, exactly like an entry card.
    /// </summary>
    internal static TimeTrackingCalendarEventViewModel From(CalendarEventInfo e)
    {
        var startLocal = e.StartLocal;
        var endLocal = e.EndLocal;
        var durationMinutes = Math.Max(1, (int)Math.Round((endLocal - startLocal).TotalMinutes));

        var isAllDay = startLocal.TimeOfDay == TimeSpan.Zero
                       && endLocal.TimeOfDay == TimeSpan.Zero
                       && endLocal > startLocal;

        if (isAllDay)
        {
            return new TimeTrackingCalendarEventViewModel(
                DateOnly.FromDateTime(startLocal),
                0,
                TimeOnly.MinValue,
                TimeOnly.MinValue,
                0d,
                0d,
                0m,
                e.Subject,
                e.ProviderKey,
                e.AccountKey,
                e.EventId,
                durationMinutes,
                true,
                DateOnly.FromDateTime(startLocal),
                DateOnly.FromDateTime(endLocal).AddDays(-1));
        }

        var dayEnd = startLocal.Date.AddDays(1);
        var visibleEnd = endLocal <= dayEnd ? endLocal : dayEnd;
        var visibleMinutes = Math.Max(1, (int)Math.Ceiling((visibleEnd - startLocal).TotalMinutes));
        var overflowMinutes = Math.Max(0, (int)Math.Ceiling((endLocal - dayEnd).TotalMinutes));
        var overflowHours = overflowMinutes <= 0
            ? 0m
            : (decimal)Math.Ceiling(overflowMinutes / 15d) / 4m;

        var minutesFromDayStart = (int)(startLocal - startLocal.Date).TotalMinutes;
        var startSlotIndex = Math.Clamp(minutesFromDayStart / TimeTrackingGridGeometry.SlotMinutes, 0, TimeTrackingGridGeometry.SlotsPerDay - 1);
        var offsetInSlotMinutes = minutesFromDayStart % TimeTrackingGridGeometry.SlotMinutes;
        var offsetTopPx = offsetInSlotMinutes * (TimeTrackingGridGeometry.SlotHeightPx / TimeTrackingGridGeometry.SlotMinutes);
        var heightPx = Math.Max(2d, visibleMinutes * (TimeTrackingGridGeometry.SlotHeightPx / TimeTrackingGridGeometry.SlotMinutes));

        return new TimeTrackingCalendarEventViewModel(
            DateOnly.FromDateTime(startLocal),
            startSlotIndex,
            TimeOnly.FromDateTime(startLocal),
            TimeOnly.FromDateTime(visibleEnd),
            offsetTopPx,
            heightPx,
            overflowHours,
            e.Subject,
            e.ProviderKey,
            e.AccountKey,
            e.EventId,
            durationMinutes,
            false,
            DateOnly.FromDateTime(startLocal),
            DateOnly.FromDateTime(startLocal));
    }
}