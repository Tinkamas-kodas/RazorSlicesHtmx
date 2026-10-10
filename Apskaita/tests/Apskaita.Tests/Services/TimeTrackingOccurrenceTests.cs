using Apskaita.Web.Features.TimeTracking.Models;
using Apskaita.Web.Features.TimeTracking.Services;

namespace Apskaita.Tests.Services;

public sealed class TimeTrackingOccurrenceTests
{
    private readonly TimeTrackingContentService _service = new();

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
        var occurrences = _service.CreateOccurrences(Request(false, null, null));

        Assert.Single(occurrences);
        Assert.Equal(BaseDate.ToDateTime(BaseTime), occurrences[0]);
    }

    [Fact]
    public void Repeating_Weekly_UntilThreeWeeksLater_ProducesFour()
    {
        var occurrences = _service.CreateOccurrences(Request(true, 1, BaseDate.AddDays(21)));

        Assert.Equal(4, occurrences.Count);
        Assert.Equal(BaseDate, DateOnly.FromDateTime(occurrences[0]));
        Assert.Equal(BaseDate.AddDays(21), DateOnly.FromDateTime(occurrences[3]));
    }

    [Fact]
    public void Repeating_BiWeekly_UntilThirtyDaysLater_ProducesThree()
    {
        var occurrences = _service.CreateOccurrences(Request(true, 2, BaseDate.AddDays(30)));

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(BaseDate.AddDays(14), DateOnly.FromDateTime(occurrences[1]));
        Assert.Equal(BaseDate.AddDays(28), DateOnly.FromDateTime(occurrences[2]));
    }

    [Fact]
    public void Repeating_MissingUntil_GuardReturnsSingle()
    {
        var occurrences = _service.CreateOccurrences(Request(true, 1, null));

        Assert.Single(occurrences);
    }

    [Fact]
    public void Repeating_MaxSpan_ProducesMaxOccurrences()
    {
        var occurrences = _service.CreateOccurrences(Request(true, 1, BaseDate.AddDays(TimeTrackingLimits.MaxRepeatSpanDays)));

        Assert.Equal(TimeTrackingLimits.MaxOccurrences, occurrences.Count);
        Assert.Equal(BaseDate.AddDays(728), DateOnly.FromDateTime(occurrences[^1]));
    }

    [Fact]
    public void Repeating_PreservesTimeOfDay()
    {
        var occurrences = _service.CreateOccurrences(Request(true, 1, BaseDate.AddDays(21)));

        foreach (var o in occurrences)
        {
            Assert.Equal(BaseTime, TimeOnly.FromDateTime(o));
        }
    }
}
