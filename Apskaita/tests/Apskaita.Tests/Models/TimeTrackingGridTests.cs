using Apskaita.Feature.TimeTracking.Models;
using Apskaita.Shared.Calendar;

namespace Apskaita.Tests.Models;

/// <summary>
/// Pure grid behavior (extracted from the week endpoint): day columns, slot rows, entry-card geometry
/// and the calendar-event mapping. All cases use a zero display offset so UTC equals wall time.
/// </summary>
public sealed class TimeTrackingGridTests
{
    private static readonly TimeSpan Zero = TimeSpan.Zero;
    private static readonly DateTime BaseDay = new(2026, 10, 5); // Monday

    // ---- TimeTrackingDayModel.WeekOf ----

    [Fact]
    public void WeekOf_ReturnsSevenDaysFromMonday()
    {
        var days = TimeTrackingDayModel.WeekOf(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7));

        Assert.Equal(7, days.Length);
        Assert.Equal(new DateOnly(2026, 10, 5), days[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 11), days[6].Date);
        Assert.Equal("Mon 05.10", days[0].Label);
        Assert.Equal("Wed 07.10", days[2].Label);
    }

    [Fact]
    public void WeekOf_MarksOnlyToday()
    {
        var days = TimeTrackingDayModel.WeekOf(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7));

        Assert.True(days[2].IsToday);
        Assert.Equal(1, days.Count(d => d.IsToday));
    }

    // ---- TimeTrackingSlotModel.WorkSlots ----

    [Fact]
    public void WorkSlots_ReturnsFortyEightHalfHourSlots()
    {
        var slots = TimeTrackingSlotModel.WorkSlots();

        Assert.Equal(48, slots.Length);
        Assert.Equal(new TimeOnly(0, 0), slots[0].Start);
        Assert.Equal(new TimeOnly(23, 30), slots[47].Start);
        Assert.Equal(TimeTrackingGridGeometry.SlotMinutes, (slots[1].Start - slots[0].Start).TotalMinutes);
    }

    [Fact]
    public void WorkSlots_FlagsWorkBandEighteen()
    {
        var slots = TimeTrackingSlotModel.WorkSlots();

        Assert.False(slots[0].IsWorkHours);          // 00:00
        Assert.True(slots[16].IsWorkHours);          // 08:00
        Assert.True(slots[33].IsWorkHours);          // 16:30
        Assert.False(slots[34].IsWorkHours);         // 17:00 is exclusive
    }

    // ---- TimeTrackingGridGeometry.DescriptionLineClamp ----

    [Theory]
    [InlineData(2, 1)]        // tiny card still shows one line
    [InlineData(100, 5)]
    [InlineData(400, 24)]     // capped
    public void DescriptionLineClamp_ReservesHeaderAndCaps(double height, int expected) =>
        Assert.Equal(expected, TimeTrackingGridGeometry.DescriptionLineClamp(height));

    // ---- TimeTrackingEntryViewModel.From ----

    [Fact]
    public void EntryViewModel_From_PlacesSlotAndHeight()
    {
        var row = new TimeTrackingEntryRow(Guid.NewGuid(), BaseDay.AddHours(8), 30, "worked", "#F4B6C2");

        var card = TimeTrackingEntryViewModel.From(row, Zero);

        Assert.Equal(16, card.StartSlotIndex);                    // 08:00
        Assert.Equal(new TimeOnly(8, 0), card.Start);
        Assert.Equal(new TimeOnly(8, 30), card.DisplayEnd);
        Assert.Equal(new DateOnly(2026, 10, 5), card.StartDate);
        Assert.Equal(0d, card.OffsetTopPx);
        Assert.Equal(34d, card.HeightPx);
        Assert.Equal(0m, card.OverflowHours);
        Assert.Equal("worked", card.Description);
    }

    [Fact]
    public void EntryViewModel_From_ClipsAtMidnightAndReportsOverflow()
    {
        // 23:30 + 60 min: 30 visible in the day, 30 overflowed → 0.5h.
        var row = new TimeTrackingEntryRow(Guid.NewGuid(), BaseDay.AddHours(23).AddMinutes(30), 60, "night", "#F4B6C2");

        var card = TimeTrackingEntryViewModel.From(row, Zero);

        Assert.Equal(47, card.StartSlotIndex);
        Assert.Equal(34d, card.HeightPx);
        Assert.Equal(0.5m, card.OverflowHours);
    }

    [Fact]
    public void EntryViewModel_From_FallsBackOnBlankDescription()
    {
        var row = new TimeTrackingEntryRow(Guid.NewGuid(), BaseDay.AddHours(8), 30, "   ", "#F4B6C2");

        var card = TimeTrackingEntryViewModel.From(row, Zero);

        Assert.Equal("No description", card.Description);
    }

    [Fact]
    public void EntryViewModel_From_ClampsDurationBounds()
    {
        var row = new TimeTrackingEntryRow(Guid.NewGuid(), BaseDay.AddHours(8), 9999, "long", "#F4B6C2");

        var card = TimeTrackingEntryViewModel.From(row, Zero);

        // Duration clamps to MaxDurationMinutes (1440 = 24h): the card fills the day column and the
        // remainder past midnight is reported as overflow.
        Assert.Equal(new TimeOnly(8, 0), card.DisplayEnd);
        Assert.Equal(1088d, card.HeightPx);
        Assert.Equal(8m, card.OverflowHours);
    }

    // ---- TimeTrackingCalendarEventViewModel.From ----

    [Fact]
    public void CalendarEventViewModel_From_MapsIdentityDurationAndGeometry()
    {
        var info = new CalendarEventInfo(
            "Standup",
            new DateTime(2026, 10, 13, 9, 45, 0),
            new DateTime(2026, 10, 13, 10, 30, 0),
            "Room 1",
            IsOnline: true,
            "Dov",
            "outlook365",
            "a@b.lt",
            "evt-1");

        var vm = TimeTrackingCalendarEventViewModel.From(info);

        Assert.Equal(new DateOnly(2026, 10, 13), vm.StartDate);
        Assert.Equal(new TimeOnly(9, 45), vm.Start);
        Assert.Equal(new TimeOnly(10, 30), vm.DisplayEnd);
        Assert.Equal(45, vm.DurationMinutes);
        Assert.Equal("outlook365", vm.ProviderKey);
        Assert.Equal("a@b.lt", vm.AccountKey);
        Assert.Equal("evt-1", vm.EventId);
        Assert.False(vm.IsAllDay);
        // 09:45 → slot 19 (585/30), 15 min into it → 17px offset; 45 min → 51px height.
        Assert.Equal(19, vm.StartSlotIndex);
        Assert.Equal(17d, vm.OffsetTopPx);
        Assert.Equal(51d, vm.HeightPx);
        Assert.Equal(0m, vm.OverflowHours);
    }

    [Fact]
    public void CalendarEventViewModel_From_KeepsMinimumOneMinute()
    {
        var info = new CalendarEventInfo("X", new DateTime(2026, 10, 13, 9, 0, 0), new DateTime(2026, 10, 13, 9, 0, 10), null, false, null, "outlook365", "a", "e");

        var vm = TimeTrackingCalendarEventViewModel.From(info);

        Assert.Equal(1, vm.DurationMinutes);
        Assert.False(vm.IsAllDay);
    }

    [Fact]
    public void CalendarEventViewModel_From_AllDayEventSpansDays()
    {
        // Midnight-to-midnight over two days → all-day band covering both day columns.
        var info = new CalendarEventInfo("Holiday", new DateTime(2026, 10, 13), new DateTime(2026, 10, 15), null, false, null, "outlook365", "a", "e");

        var vm = TimeTrackingCalendarEventViewModel.From(info);

        Assert.True(vm.IsAllDay);
        Assert.Equal(new DateOnly(2026, 10, 13), vm.AllDayFirstDate);
        Assert.Equal(new DateOnly(2026, 10, 14), vm.AllDayLastDate);
    }

    [Fact]
    public void CalendarEventViewModel_From_CrossMidnightEventClipsAndReportsOverflow()
    {
        // 23:30 + 60 min: 30 visible in the day, 30 overflowed → 0.5h (same math as an entry card).
        var info = new CalendarEventInfo("Late", new DateTime(2026, 10, 13, 23, 30, 0), new DateTime(2026, 10, 14, 0, 30, 0), null, false, null, "outlook365", "a", "e");

        var vm = TimeTrackingCalendarEventViewModel.From(info);

        Assert.False(vm.IsAllDay);
        Assert.Equal(47, vm.StartSlotIndex);
        Assert.Equal(34d, vm.HeightPx);
        Assert.Equal(0.5m, vm.OverflowHours);
    }
}
