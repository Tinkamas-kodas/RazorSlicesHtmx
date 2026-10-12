namespace Apskaita.Web.Features.TimeTracking.Models;

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

public sealed record TimeTrackingDayModel(
    DateOnly Date,
    string Label,
    bool IsToday);

public sealed record TimeTrackingSlotModel(
    int Index,
    TimeOnly Start,
    TimeOnly End,
    bool IsWorkHours);

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
    int DescriptionLineClamp);

/// <summary>
/// A calendar event rendered in the time tracking side panel (display-only, REQ-008). Times are local;
/// <see cref="DayDate"/> groups the event under its day header.
/// </summary>
public sealed record TimeTrackingCalendarEventViewModel(
    DateOnly DayDate,
    string Subject,
    TimeOnly Start,
    TimeOnly End,
    string? Location,
    bool IsOnline,
    string? OrganizerName);
