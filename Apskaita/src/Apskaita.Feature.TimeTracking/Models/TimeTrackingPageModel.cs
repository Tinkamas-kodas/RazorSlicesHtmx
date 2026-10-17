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