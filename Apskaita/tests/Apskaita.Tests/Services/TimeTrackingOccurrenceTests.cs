using Apskaita.Feature.TimeTracking.Models;

namespace Apskaita.Tests.Services;

/// <summary>
/// Recurrence generation is pure request behavior on <see cref="TimeTrackingModel"/> (the content
/// service is dissolved): these lock the occurrence sequence invariants.
/// </summary>
public sealed class TimeTrackingOccurrenceTests
{
    private static readonly DateOnly BaseDate = new(2026, 10, 5);
    private static readonly TimeOnly BaseTime = new(13, 0);

    private static TimeTrackingModel Request(bool repeat, int? weeks, DateOnly? until) => new()
    {
        Date = BaseDate,
        Start = BaseTime,
        DurationMinutes = 30,
        ProjectId = Guid.NewGuid(),
        Description = "worked",
        Repeat = repeat,
        RepeatEveryWeeks = weeks,
        RepeatUntil = until
    };

    [Fact]
    public void NonRepeating_ReturnsSingleBaseOccurrence()
    {
        var occurrences = Request(false, null, null).CreateOccurrences();

        Assert.Single(occurrences);
        Assert.Equal(BaseDate.ToDateTime(BaseTime), occurrences[0]);
    }

    [Fact]
    public void Repeating_Weekly_UntilThreeWeeksLater_ProducesFour()
    {
        var occurrences = Request(true, 1, BaseDate.AddDays(21)).CreateOccurrences();

        Assert.Equal(4, occurrences.Count);
        Assert.Equal(BaseDate, DateOnly.FromDateTime(occurrences[0]));
        Assert.Equal(BaseDate.AddDays(21), DateOnly.FromDateTime(occurrences[3]));
    }

    [Fact]
    public void Repeating_BiWeekly_UntilThirtyDaysLater_ProducesThree()
    {
        var occurrences = Request(true, 2, BaseDate.AddDays(30)).CreateOccurrences();

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(BaseDate.AddDays(14), DateOnly.FromDateTime(occurrences[1]));
        Assert.Equal(BaseDate.AddDays(28), DateOnly.FromDateTime(occurrences[2]));
    }

    [Fact]
    public void Repeating_MissingUntil_GuardReturnsSingle()
    {
        var occurrences = Request(true, 1, null).CreateOccurrences();

        Assert.Single(occurrences);
    }

    [Fact]
    public void Repeating_MaxSpan_ProducesMaxOccurrences()
    {
        var occurrences = Request(true, 1, BaseDate.AddDays(TimeTrackingLimits.MaxRepeatSpanDays)).CreateOccurrences();

        Assert.Equal(TimeTrackingLimits.MaxOccurrences, occurrences.Count);
        Assert.Equal(BaseDate.AddDays(728), DateOnly.FromDateTime(occurrences[^1]));
    }

    [Fact]
    public void Repeating_PreservesTimeOfDay()
    {
        var occurrences = Request(true, 1, BaseDate.AddDays(21)).CreateOccurrences();

        foreach (var o in occurrences)
        {
            Assert.Equal(BaseTime, TimeOnly.FromDateTime(o));
        }
    }
}
