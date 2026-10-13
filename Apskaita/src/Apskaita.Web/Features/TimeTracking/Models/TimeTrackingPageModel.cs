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
    bool IsTracked);
