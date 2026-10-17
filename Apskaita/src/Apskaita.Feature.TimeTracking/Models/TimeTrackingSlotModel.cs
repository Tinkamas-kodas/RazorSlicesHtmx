namespace Apskaita.Feature.TimeTracking.Models;

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