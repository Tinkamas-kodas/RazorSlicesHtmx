using System.Globalization;
using System.Text;
using Apskaita.Entities;
using Ical.Net;
using Ical.Net.DataTypes;

namespace Apskaita.Shared.Calendar;

/// <summary>
/// One normalized <c>VEVENT</c> occurrence parsed from an uploaded <c>.ics</c> file. Times are UTC;
/// an all-day event has <see cref="EndUtc"/> = null and a midnight-UTC <see cref="StartUtc"/> so the
/// panel can render it as a date-only row.
/// </summary>
/// <param name="ExternalEventId">This occurrence's identity: the RFC 5545 <c>UID</c>, or
/// <c>&lt;UID&gt;@&lt;utc start&gt;</c> for an occurrence of a recurring series (already clamped).</param>
/// <param name="StartUtc">Occurrence start in UTC.</param>
/// <param name="EndUtc">Occurrence end in UTC; null for all-day events.</param>
/// <param name="Subject">Cleaned <c>SUMMARY</c>; never empty ("(No subject)" for untitled events).</param>
/// <param name="Location">Cleaned <c>LOCATION</c>, or null.</param>
/// <param name="OrganizerName">Cleaned <c>ORGANIZER</c> common name, or null.</param>
/// <param name="GroupId">The series <c>UID</c> the occurrence was expanded from; null for a single event.</param>
public sealed record IcsParsedEvent(
    string ExternalEventId,
    DateTime StartUtc,
    DateTime? EndUtc,
    string Subject,
    string? Location,
    string? OrganizerName,
    string? GroupId = null);

/// <summary>
/// Outcome of parsing an uploaded file. <see cref="Error"/> is a user-presentable reason and
/// <see cref="Events"/> is empty when it is set (nothing may be stored). <see cref="Warning"/> is a
/// non-fatal note (skipped or truncated events) shown next to a successful import.
/// </summary>
public sealed record IcsImportResult(
    bool Success,
    IReadOnlyList<IcsParsedEvent> Events,
    string? Error,
    string? Warning)
{
    public static IcsImportResult Failed(string error) => new(false, [], error, null);
}

/// <summary>
/// Parses an uploaded <c>.ics</c> (RFC 5545 <c>VCALENDAR</c>) into <see cref="IcsParsedEvent"/> rows
/// (feature-calendar-connectors-1 §4.4). <c>Ical.Net</c> does the RFC mechanics — line unfolding, quoted
/// parameters, <c>VTIMEZONE</c>/DST, <c>RRULE</c> expansion, <c>EXDATE</c>/<c>RDATE</c> and
/// <c>RECURRENCE-ID</c> overrides; this class adds the product rules:
/// <list type="bullet">
/// <item>the upload size cap and the <c>BEGIN:VCALENDAR</c> sniff (REQ-015);</item>
/// <item>a recurring event is expanded in full when the file bounds it (<c>UNTIL</c>/<c>COUNT</c>, or an
/// explicit <c>RDATE</c> list); only an <b>unbounded</b> <c>RRULE</c> is cut at
/// <see cref="RecurrenceHorizonDays"/> days after today, because its occurrence set is infinite;</item>
/// <item>every imported string is treated as untrusted: control characters are stripped and lengths are
/// clamped to the storage limits (REQ-023);</item>
/// <item>an occurrence of a recurring series gets its own identity (<c>&lt;UID&gt;@&lt;utc start&gt;</c>) while
/// <see cref="IcsParsedEvent.GroupId"/> keeps the shared series <c>UID</c>, so a time entry can link to one
/// date of a series instead of to the whole series;</item>
/// <item>only <c>VEVENT</c> is read — <c>VTODO</c>, <c>VJOURNAL</c>, <c>VFREEBUSY</c>, descriptions and
/// attendees are ignored.</item>
/// </list>
/// Stateless; registered as a singleton.
/// </summary>
public sealed class IcsCalendarImporter
{
    /// <summary>Stable provider key of the imported-calendar connector (matches <c>CalendarProvider.Ics</c>).</summary>
    public const string ProviderKey = "ics";

    /// <summary>Largest accepted upload, in bytes (REQ-015).</summary>
    public const int MaxUploadBytes = 2 * 1024 * 1024;

    /// <summary>
    /// How far ahead of today an <b>unbounded</b> recurring event is expanded at import time. A rule that
    /// states its own end (<c>UNTIL</c>/<c>COUNT</c>) is expanded completely — see <see cref="IsBounded"/>.
    /// </summary>
    public const int RecurrenceHorizonDays = 90;

    /// <summary>Safety valve for pathological files; a normal export never reaches it.</summary>
    public const int MaxStoredOccurrences = 20_000;

    private const string NoSubject = "(No subject)";

    /// <summary>Suffix that turns a series <c>UID</c> into a per-occurrence identity.</summary>
    private const char OccurrenceIdSeparator = '@';

    private const string OccurrenceIdFormat = "yyyyMMddTHHmmssZ";

    /// <summary>
    /// Reads and parses <paramref name="stream"/>. Never throws: a malformed file, an oversized upload, or
    /// a parser failure all come back as <see cref="IcsImportResult.Failed"/> with a reason to show the user.
    /// </summary>
    public IcsImportResult Import(Stream stream)
    {
        string text;
        try
        {
            text = ReadWithLimit(stream, MaxUploadBytes);
        }
        catch (IcsUploadTooLargeException)
        {
            return IcsImportResult.Failed($"The file is larger than {MaxUploadBytes / (1024 * 1024)} MB.");
        }
        catch (Exception ex)
        {
            return IcsImportResult.Failed("The file could not be read: " + ex.GetType().Name);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return IcsImportResult.Failed("The file is empty.");
        }

        if (!StartsAsVCalendar(text))
        {
            return IcsImportResult.Failed("The file is not an .ics calendar (no BEGIN:VCALENDAR).");
        }

        CalendarCollection calendar;
        try
        {
            calendar = CalendarCollection.Load(new StringReader(text));
        }
        catch (Exception ex)
        {
            return IcsImportResult.Failed("The calendar could not be parsed: " + ex.GetType().Name);
        }

        var events = new List<IcsParsedEvent>();
        var skipped = 0;
        var truncated = false;
        var horizonCut = false;
        var horizonEnd = DateTime.UtcNow.Date.AddDays(RecurrenceHorizonDays);

        foreach (var part in calendar)
        {
            foreach (var evt in part.Events)
            {
                // A RECURRENCE-ID row is an override of another row with the same UID. The master's
                // expansion below already produces one row per occurrence (each with its own identity), so
                // storing the override as well would duplicate it.
                if (evt.RecurrenceIdentifier is not null)
                {
                    continue;
                }

                // A recurring series shares one UID, so its occurrences are identified by UID + start; the
                // series UID is kept separately as the group id.
                var recurring = HasRecurrence(evt);
                var bounded = IsBounded(evt);
                var uid = Clean(evt.Uid, recurring ? MaxSeriesIdLength : FieldLengths.CalendarImportedEvent.ExternalEventId);
                if (uid is null)
                {
                    skipped++;
                    continue;
                }

                var subject = Clean(evt.Summary, FieldLengths.CalendarImportedEvent.Subject) ?? NoSubject;
                var location = Clean(evt.Location, FieldLengths.CalendarImportedEvent.Location);
                var organizer = Clean(evt.Organizer?.CommonName, FieldLengths.CalendarImportedEvent.OrganizerName);

                try
                {
                    if (evt.Start is null)
                    {
                        skipped++;
                        continue;
                    }

                    var allDay = !evt.Start.HasTime;

                    if (recurring)
                    {
                        foreach (var occurrence in evt.GetOccurrences())
                        {
                            var startUtc = ToUtc(occurrence.Period.StartTime);

                            // The horizon only guards infinite rules; a rule that says when it ends is
                            // expanded to its own end.
                            if (!bounded && startUtc.Date > horizonEnd)
                            {
                                horizonCut = true;
                                break;
                            }

                            var occurrenceEnd = allDay ? null : ToUtcOrNull(occurrence.Period.EffectiveEndTime);

                            if (!Add(events, new IcsParsedEvent(
                                OccurrenceId(uid, startUtc),
                                startUtc,
                                occurrenceEnd,
                                subject,
                                location,
                                organizer,
                                uid)))
                            {
                                truncated = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var startUtc = ToUtc(evt.Start);
                        var endUtc = evt.End is null ? (DateTime?)null : ToUtc(evt.End);
                        if (allDay)
                        {
                            endUtc = null;
                        }
                        else if (endUtc is not null && endUtc <= startUtc)
                        {
                            endUtc = null;
                        }

                        if (!Add(events, new IcsParsedEvent(uid, startUtc, endUtc, subject, location, organizer)))
                        {
                            truncated = true;
                        }
                    }
                }
                catch (Exception)
                {
                    // One broken event (an unsupported rule form, a corrupt date) must not lose the file.
                    skipped++;
                }
            }
        }

        if (events.Count == 0)
        {
            return IcsImportResult.Failed("The calendar contains no events.");
        }

        var warnings = new List<string>();
        if (skipped > 0)
        {
            warnings.Add($"{skipped} event(s) were skipped (no UID or unreadable dates).");
        }

        if (truncated)
        {
            warnings.Add($"Only the first {MaxStoredOccurrences} occurrences were stored.");
        }

        if (horizonCut)
        {
            warnings.Add($"Recurring events without an end date are expanded {RecurrenceHorizonDays} days ahead — re-import to extend them.");
        }

        return new IcsImportResult(true, events, null, string.Join(" ", warnings));
    }

    /// <summary>True when the first non-empty line opens a VCALENDAR (REQ-015).</summary>
    private static bool StartsAsVCalendar(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim().TrimStart('\uFEFF');
            if (trimmed.Length == 0)
            {
                continue;
            }

            return trimmed.StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Reads at most <paramref name="limitBytes"/>; throws <see cref="IcsUploadTooLargeException"/> when the
    /// stream is longer, so an oversized file never reaches the parser.
    /// </summary>
    private static string ReadWithLimit(Stream stream, int limitBytes)
    {
        using var buffer = new MemoryStream(limitBytes + 1);
        var chunk = new byte[81920];
        var total = 0;

        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > limitBytes)
            {
                throw new IcsUploadTooLargeException();
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static bool HasRecurrence(Ical.Net.CalendarComponents.CalendarEvent evt) =>
        evt.RecurrenceRule is not null || evt.RecurrenceDates.GetAllDates().Any();

    /// <summary>
    /// True when the file itself states where the series ends: an <c>RRULE</c> with <c>UNTIL</c> or
    /// <c>COUNT</c>, or an <c>RDATE</c>-only event (whose dates are all listed). Such a series is expanded
    /// completely; only an endless rule is stopped at <see cref="RecurrenceHorizonDays"/>.
    /// </summary>
    private static bool IsBounded(Ical.Net.CalendarComponents.CalendarEvent evt) =>
        evt.RecurrenceRule is null || evt.RecurrenceRule.Until is not null || evt.RecurrenceRule.Count > 0;

    /// <summary>Longest series <c>UID</c> that still leaves room for the occurrence suffix.</summary>
    private static readonly int MaxSeriesIdLength =
        FieldLengths.CalendarImportedEvent.ExternalEventId - OccurrenceIdFormat.Length - 1;

    /// <summary>Per-occurrence identity of a recurring event: the series UID plus its UTC start.</summary>
    private static string OccurrenceId(string seriesId, DateTime startUtc) =>
        seriesId + OccurrenceIdSeparator + startUtc.ToString(OccurrenceIdFormat, CultureInfo.InvariantCulture);

    private static bool Add(List<IcsParsedEvent> events, IcsParsedEvent evt)
    {
        if (events.Count >= MaxStoredOccurrences)
        {
            return false;
        }

        events.Add(evt);
        return true;
    }

    /// <summary>UTC instant of a parsed calendar date; date-only values become midnight UTC.</summary>
    private static DateTime ToUtc(CalDateTime value)
    {
        var utc = value.AsUtc.ToUniversalTime();
        var normalized = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return value.HasTime ? normalized : normalized.Date;
    }

    /// <summary>UTC instant of an optional calendar date; null stays null.</summary>
    private static DateTime? ToUtcOrNull(CalDateTime? value) => value is null ? null : ToUtc(value);

    /// <summary>
    /// Strips control characters and whitespace runs, then clamps to <paramref name="maxLength"/>
    /// (REQ-023: imported text is untrusted and must never carry markup or control bytes into storage).
    /// </summary>
    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var ch in value)
        {
            if (char.IsControl(ch) || char.IsWhiteSpace(ch) || ch == '\uFEFF' || ch == '\u200B')
            {
                if (builder.Length > 0)
                {
                    pendingSpace = true;
                }

                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);

            if (builder.Length >= maxLength)
            {
                break;
            }
        }

        var result = builder.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    private sealed class IcsUploadTooLargeException() : Exception("The uploaded calendar file is too large.");

}
