using Apskaita.Entities.Contracts;
using Apskaita.Feature.Account.Persistence;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Account.Services;

/// <summary>
/// Reads stored <c>.ics</c> events for the current user's imported calendars (REQ-020). The rows are
/// already normalized UTC instants, so a read is one indexed query per week — no parser, no file, and no
/// network call. Events keep the identity triple (<c>ics</c>, calendar name, event UID) so clicking one in
/// the time tracking panel links a time entry exactly like a Graph event.
/// </summary>
internal sealed class IcsEventReader(
    IcsConnectorStore store,
    IQuery<CalendarImportedEvent> events,
    IHttpContextAccessor httpContextAccessor) : ICalendarEventReader
{
    public const string ProviderKey = IcsCalendarImporter.ProviderKey;

    public async Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct)
    {
        var connectors = await store.GetOwnAsync(ct);
        if (connectors.Count == 0)
        {
            return new CalendarReadResult(false, [], null);
        }

        var accountByKey = connectors.ToDictionary(c => c.Id, c => c.AccountKey);
        var connectorIds = accountByKey.Keys.ToArray();

        var offset = httpContextAccessor.HttpContext is { } ctx
            ? BrowserTimeZone.GetDisplayOffset(ctx)
            : TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

        // The week is expressed in display-zone dates; the query bounds shift to UTC the same way the
        // Graph reader does, so both providers agree on which events belong to the week.
        var fromUtc = BrowserTimeZone.WallToUtc(fromLocal.ToDateTime(TimeOnly.MinValue), offset);
        var toUtc = BrowserTimeZone.WallToUtc(toLocal.ToDateTime(TimeOnly.MaxValue), offset);

        var rows = await events.All()
            // ReSharper disable once CSharp14OverloadResolutionWithSpanBreakingChange
            .Where(e => connectorIds.Contains(e.ConnectorId) && e.StartUtc >= fromUtc && e.StartUtc <= toUtc)
            .ToListAsync(ct);

        var result = rows
            .Select(e => ToInfo(e, offset, accountByKey))
            .OrderBy(e => e.StartLocal)
            .ThenBy(e => e.Subject, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CalendarReadResult(true, result, null);
    }

    private static CalendarEventInfo ToInfo(
        CalendarImportedEvent e,
        TimeSpan offset,
        IReadOnlyDictionary<Guid, string> accountByKey)
    {
        // An all-day row stores midnight UTC of its date and no end: keep the date itself (shifting it by
        // the display offset could move it to the previous day) and end it at the next midnight.
        var startLocal = e.EndUtc is null
            ? e.StartUtc.Date
            : BrowserTimeZone.UtcToWall(e.StartUtc, offset);
        var endLocal = e.EndUtc is null
            ? startLocal.AddDays(1)
            : BrowserTimeZone.UtcToWall(e.EndUtc.Value, offset);

        return new CalendarEventInfo(
            e.Subject,
            startLocal,
            endLocal,
            e.Location,
            e.IsOnline,
            e.OrganizerName,
            ProviderKey,
            accountByKey.TryGetValue(e.ConnectorId, out var accountKey) ? accountKey : string.Empty,
            e.ExternalEventId);
    }
}
