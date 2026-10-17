using System.Text;
using Apskaita.Shared.Calendar;

namespace Apskaita.Tests.Services;

/// <summary>
/// Unit tests for the <c>.ics</c> importer (feature-calendar-connectors-1 §4.4): the RFC mechanics come
/// from <c>Ical.Net</c>, so these assert the product rules on top — the upload sniff and size cap, the
/// recurrence horizon, all-day storage shape, untrusted-text cleaning, and per-event fault isolation.
/// Fixtures are built inline with CRLF line endings, like a real export.
/// </summary>
public sealed class IcsCalendarImporterTests
{
    private static readonly DateTime TodayUtc = DateTime.UtcNow.Date;

    private static string Day(int offsetDays, string time = "090000") =>
        TodayUtc.AddDays(offsetDays).ToString("yyyyMMdd") + "T" + time + "Z";

    private static IcsImportResult Import(params string[] lines)
    {
        var text = string.Join("\r\n", lines) + "\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return new IcsCalendarImporter().Import(stream);
    }

    private static IcsImportResult Calendar(params string[][] events) => Import(CalendarLines(events));

    private static string[] CalendarLines(string[][] events) =>
    [
        "BEGIN:VCALENDAR",
        "VERSION:2.0",
        "PRODID:-//Apskaita//Tests//EN",
        .. events.SelectMany(e => e),
        "END:VCALENDAR"
    ];

    private static string[] Event(string uid, string start, string? end, params string[] extra)
    {
        var lines = new List<string> { "BEGIN:VEVENT", $"UID:{uid}", $"DTSTART:{start}" };
        if (end is not null)
        {
            lines.Add($"DTEND:{end}");
        }

        lines.AddRange(extra);
        lines.Add("END:VEVENT");
        return [.. lines];
    }

    [Fact]
    public void Import_TimedEvent_ReturnsNormalizedUtcRow()
    {
        var result = Calendar(Event(
            "one",
            Day(0),
            Day(0, "100000"),
            "SUMMARY:Team sync",
            "LOCATION:Vilnius office",
            "ORGANIZER;CN=Jonas Jonaitis:mailto:jonas@example.com"));

        Assert.True(result.Success, result.Error);
        var evt = Assert.Single(result.Events);
        Assert.Equal("one", evt.ExternalEventId);
        Assert.Equal("Team sync", evt.Subject);
        Assert.Equal("Vilnius office", evt.Location);
        Assert.Equal("Jonas Jonaitis", evt.OrganizerName);
        Assert.Equal(new DateTime(TodayUtc.Year, TodayUtc.Month, TodayUtc.Day, 9, 0, 0, DateTimeKind.Utc), evt.StartUtc);
        Assert.Equal(new DateTime(TodayUtc.Year, TodayUtc.Month, TodayUtc.Day, 10, 0, 0, DateTimeKind.Utc), evt.EndUtc);
        Assert.Equal(DateTimeKind.Utc, evt.StartUtc.Kind);
    }

    [Fact]
    public void Import_AllDayEvent_StoresMidnightUtcWithNoEnd()
    {
        var result = Calendar(Event("allday", TodayUtc.ToString("yyyyMMdd"), null, "SUMMARY:Holiday"));

        Assert.True(result.Success, result.Error);
        var evt = Assert.Single(result.Events);
        Assert.Equal(TodayUtc, evt.StartUtc);
        Assert.Null(evt.EndUtc);
    }

    [Fact]
    public void Import_UntitledEvent_GetsPlaceholderSubject()
    {
        var result = Calendar(Event("nosummary", Day(0), Day(0, "093000")));

        Assert.True(result.Success, result.Error);
        Assert.Equal("(No subject)", Assert.Single(result.Events).Subject);
    }

    [Fact]
    public void Import_TzidWithVTimezone_ConvertsToUtc()
    {
        // A July date in Europe/Vilnius is summer time (+03:00), so 08:00 local is 05:00 UTC.
        var local = new DateTime(TodayUtc.Year, 7, 15, 8, 0, 0, DateTimeKind.Unspecified);

        var result = Import(
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Apskaita//Tests//EN",
            "BEGIN:VTIMEZONE",
            "TZID:Europe/Vilnius",
            "BEGIN:STANDARD",
            "DTSTART:19701025T040000",
            "RRULE:FREQ=YEARLY;BYDAY=-1SU;BYMONTH=10",
            "TZOFFSETFROM:+0300",
            "TZOFFSETTO:+0200",
            "TZNAME:EET",
            "END:STANDARD",
            "BEGIN:DAYLIGHT",
            "DTSTART:19700329T030000",
            "RRULE:FREQ=YEARLY;BYDAY=-1SU;BYMONTH=3",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0300",
            "TZNAME:EEST",
            "END:DAYLIGHT",
            "END:VTIMEZONE",
            "BEGIN:VEVENT",
            "UID:tz",
            $"DTSTART;TZID=Europe/Vilnius:{local:yyyyMMdd}T{local:HHmmss}",
            "SUMMARY:Local meeting",
            "END:VEVENT",
            "END:VCALENDAR");

        Assert.True(result.Success, result.Error);
        var evt = Assert.Single(result.Events);
        Assert.Equal(new DateTime(local.Year, 7, 15, 5, 0, 0, DateTimeKind.Utc), evt.StartUtc);
    }

    [Fact]
    public void Import_FoldedLines_UnfoldBeforeParsing()
    {
        var result = Calendar(Event(
            "folded",
            Day(0),
            Day(0, "093000"),
            "SUMMARY:A very long subject that has been folded",
            " across two lines by the exporter"));

        Assert.True(result.Success, result.Error);
        Assert.Equal("A very long subject that has been foldedacross two lines by the exporter",
            Assert.Single(result.Events).Subject);
    }

    [Fact]
    public void Import_WeeklyRecurrenceWithCount_ExpandsEveryOccurrence()
    {
        var result = Calendar(Event("weekly", Day(-7), Day(-7, "093000"), "SUMMARY:Standup", "RRULE:FREQ=WEEKLY;COUNT=4"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(4, result.Events.Count);
        Assert.Equal(result.Events.Select(e => e.StartUtc).OrderBy(d => d), result.Events.Select(e => e.StartUtc));

        // One series, four occurrences: every row is addressable on its own, and they share the series id.
        Assert.Equal(4, result.Events.Select(e => e.ExternalEventId).Distinct().Count());
        Assert.All(result.Events, e => Assert.StartsWith("weekly@", e.ExternalEventId));
        Assert.All(result.Events, e => Assert.Equal("weekly", e.GroupId));
    }

    [Fact]
    public void Import_SingleEvent_KeepsTheRawUid_AndHasNoGroup()
    {
        var result = Calendar(Event("one", Day(0), Day(0, "093000"), "SUMMARY:One off"));

        Assert.True(result.Success, result.Error);
        var evt = Assert.Single(result.Events);
        Assert.Equal("one", evt.ExternalEventId);
        Assert.Null(evt.GroupId);
    }

    [Fact]
    public void Import_RecurrenceDates_AlsoGetPerOccurrenceIds()
    {
        var result = Calendar(Event(
            "rdate",
            Day(0),
            Day(0, "093000"),
            "SUMMARY:Ad-hoc series",
            $"RDATE:{Day(14)}"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal(2, result.Events.Select(e => e.ExternalEventId).Distinct().Count());
        Assert.All(result.Events, e => Assert.Equal("rdate", e.GroupId));
    }

    [Fact]
    public void Import_LongUidInARecurringSeries_StillFitsTheColumn()
    {
        var result = Calendar(Event(
            new string('u', 300),
            Day(0),
            Day(0, "093000"),
            "SUMMARY:Long uid series",
            "RRULE:FREQ=WEEKLY;COUNT=2"));

        Assert.True(result.Success, result.Error);
        Assert.All(result.Events, e => Assert.True(e.ExternalEventId.Length <= 255, $"Length was {e.ExternalEventId.Length}."));
        Assert.Equal(2, result.Events.Select(e => e.ExternalEventId).Distinct().Count());
    }

    [Fact]
    public void Import_UnboundedRecurrence_StopsAtTheHorizon()
    {
        var result = Calendar(Event("daily", Day(-3), Day(-3, "093000"), "SUMMARY:Every day", "RRULE:FREQ=DAILY"));

        Assert.True(result.Success, result.Error);

        // Three days of history plus today..horizon (inclusive), and nothing beyond the horizon.
        var expected = 4 + IcsCalendarImporter.RecurrenceHorizonDays;
        Assert.Equal(expected, result.Events.Count);
        Assert.Equal(TodayUtc.AddDays(-3), result.Events.Min(e => e.StartUtc).Date);
        Assert.Equal(TodayUtc.AddDays(IcsCalendarImporter.RecurrenceHorizonDays), result.Events.Max(e => e.StartUtc).Date);
    }

    [Fact]
    public void Import_BoundedRecurrence_ExpandsToItsOwnEnd_BeyondTheHorizon()
    {
        // The rule states where it stops, so the horizon must not cut it: weeklies until ~200 days out.
        var until = "RRULE:FREQ=WEEKLY;UNTIL=" + TodayUtc.AddDays(203).ToString("yyyyMMdd") + "T000000Z";
        var result = Calendar(Event("bounded", Day(0), Day(0, "093000"), "SUMMARY:Until it ends", until));

        Assert.True(result.Success, result.Error);

        // Weeklies from today until today+203: the last start that fits before UNTIL is +196 (29 rows).
        Assert.Equal(29, result.Events.Count);
        Assert.Equal(TodayUtc.AddDays(196), result.Events.Max(e => e.StartUtc).Date);
        Assert.DoesNotContain("days ahead", result.Warning);

        // A recurring occurrence's end lives on Period.EffectiveEndTime, so timed rows keep their duration.
        Assert.All(result.Events, e => Assert.Equal(e.StartUtc.AddMinutes(30), e.EndUtc));
    }

    [Fact]
    public void Import_ExceptionDate_SkipsThatOccurrence()
    {
        var second = Day(7);
        var result = Calendar(Event(
            "exdate",
            Day(0),
            Day(0, "093000"),
            "SUMMARY:Weekly review",
            "RRULE:FREQ=WEEKLY;COUNT=3",
            $"EXDATE:{second}"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Events.Count);
        Assert.DoesNotContain(result.Events, e => e.StartUtc.ToString("yyyyMMddTHHmmssZ") == second);
    }

    [Fact]
    public void Import_RecurrenceIdOverride_DoesNotDuplicateTheSeries()
    {
        var result = Calendar(
            Event("master", Day(0), Day(0, "093000"), "SUMMARY:Series", "RRULE:FREQ=WEEKLY;COUNT=2"),
            [
                "BEGIN:VEVENT",
                "UID:master",
                $"DTSTART:{Day(7)}",
                $"DTEND:{Day(7, "093000")}",
                "SUMMARY:Moved occurrence",
                $"RECURRENCE-ID:{Day(7)}",
                "END:VEVENT"
            ]);

        Assert.True(result.Success, result.Error);

        // The override row never becomes its own event; the series keeps its two occurrences, each with its
        // own identity and the shared series id as group.
        Assert.Equal(2, result.Events.Count);
        Assert.All(result.Events, e => Assert.Equal("master", e.GroupId));
        Assert.Equal(2, result.Events.Select(e => e.ExternalEventId).Distinct().Count());
    }

    [Fact]
    public void Import_EventWithoutStartDate_IsSkippedWithAWarning()
    {
        var result = Calendar(
            Event("kept", Day(0), Day(0, "093000"), "SUMMARY:Kept"),
            ["BEGIN:VEVENT", "UID:nostart", "SUMMARY:No start", "END:VEVENT"]);

        Assert.True(result.Success, result.Error);
        Assert.Equal("kept", Assert.Single(result.Events).ExternalEventId);
        Assert.Contains("skipped", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_NonCalendarText_Fails()
    {
        var result = Import("hello, this is not a calendar");

        Assert.False(result.Success);
        Assert.Empty(result.Events);
        Assert.Contains(".ics", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_EmptyFile_Fails()
    {
        var result = Import(string.Empty);

        Assert.False(result.Success);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_CalendarWithoutEvents_Fails()
    {
        var result = Import(
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "BEGIN:VTODO",
            "UID:todo-1",
            "SUMMARY:Something due",
            "END:VTODO",
            "END:VCALENDAR");

        Assert.False(result.Success);
        Assert.Contains("no events", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_BeyondTheSizeCap_FailsWithoutParsing()
    {
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0" };
        lines.AddRange(Event("big", Day(0), Day(0, "093000"), "SUMMARY:Big"));
        lines.AddRange(Enumerable.Repeat("X-JUNK:" + new string('x', 200), IcsCalendarImporter.MaxUploadBytes / 207 + 10));
        lines.Add("END:VCALENDAR");

        var result = Import([.. lines]);

        Assert.False(result.Success);
        Assert.Contains("larger than", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_ControlCharacters_AreStrippedAndTextClamped()
    {
        var result = Calendar(Event(
            "dirty",
            Day(0),
            Day(0, "093000"),
            "SUMMARY:Tab\tseparated and padded  ",
            "LOCATION:" + new string('L', 600)));

        Assert.True(result.Success, result.Error);
        var evt = Assert.Single(result.Events);
        Assert.Equal("Tab separated and padded", evt.Subject);
        Assert.Equal(500, evt.Location!.Length);
    }

    [Fact]
    public void Import_MarkupInSummary_StaysPlainText()
    {
        var result = Calendar(Event("xss", Day(0), Day(0, "093000"), "SUMMARY:<script>alert(1)</script>"));

        Assert.True(result.Success, result.Error);

        // The importer never strips markup (Razor encodes on render); it must not interpret it either.
        Assert.Equal("<script>alert(1)</script>", Assert.Single(result.Events).Subject);
    }

    [Fact]
    public void Import_MultipleCalendarsInOneFile_AreMerged()
    {
        var result = Import(
        [
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            .. Event("a", Day(0), Day(0, "093000"), "SUMMARY:First"),
            "END:VCALENDAR",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            .. Event("b", Day(1), Day(1, "093000"), "SUMMARY:Second"),
            "END:VCALENDAR"
        ]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal(["a", "b"], result.Events.Select(e => e.ExternalEventId).Order());
    }
}
