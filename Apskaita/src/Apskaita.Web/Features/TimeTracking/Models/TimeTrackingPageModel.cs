namespace Apskaita.Web.Features.TimeTracking.Models;

public sealed record TimeTrackingPageModel(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    DateOnly PreviousWeekStart,
    DateOnly NextWeekStart,
    IReadOnlyList<TimeTrackingDayModel> Days,
    IReadOnlyList<TimeTrackingSlotModel> Slots,
    IReadOnlyList<TimeTrackingEntryViewModel> Entries);

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
