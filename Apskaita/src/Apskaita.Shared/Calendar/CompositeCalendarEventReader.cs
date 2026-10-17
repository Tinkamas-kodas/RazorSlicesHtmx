namespace Apskaita.Shared.Calendar;

/// <summary>
/// Merges the calendar readers of every configured provider into the single <see cref="ICalendarEventReader"/>
/// the time tracking page injects (REQ-021): events are sorted by local start, <c>HasConnector</c> is true
/// when ANY provider has a connection, and one provider's failure never blanks the panel — its notice is
/// reported while the other provider's events still render.
/// </summary>
public sealed class CompositeCalendarEventReader(IReadOnlyList<ICalendarEventReader> readers) : ICalendarEventReader
{
    public async Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct)
    {
        var results = new List<CalendarReadResult>(readers.Count);

        foreach (var reader in readers)
        {
            try
            {
                results.Add(await reader.ReadAsync(fromLocal, toLocal, ct));
            }
            catch (Exception)
            {
                // A reader contract must not throw; a broken provider is isolated, never fatal to the page.
                results.Add(new CalendarReadResult(false, [], null));
            }
        }

        var hasConnector = results.Any(r => r.HasConnector);
        var events = results
            .SelectMany(r => r.Events)
            .OrderBy(e => e.StartLocal)
            .ThenBy(e => e.Subject, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var notice = hasConnector ? results.FirstOrDefault(r => r.Notice is not null)?.Notice : null;

        return new CalendarReadResult(hasConnector, events, notice);
    }
}
