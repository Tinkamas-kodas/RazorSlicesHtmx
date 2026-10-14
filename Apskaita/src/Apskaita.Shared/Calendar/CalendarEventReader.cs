namespace Apskaita.Shared.Calendar;

/// <summary>
/// A normalized calendar event for display. Mapped from a provider's event model (currently Microsoft
/// Graph <c>calendarView</c>); times are already converted to the display zone — the browser's UTC
/// offset when its cookie is present, otherwise the server's local zone.
/// <see cref="ProviderKey"/> + <see cref="AccountKey"/> + <see cref="EventId"/> form the stable
/// identity triple used to link time entries to an event (see <c>CalendarEventReference</c>).
/// Display-only otherwise: the UI must not offer actions derived from these events beyond the
/// create/edit dialog flow.
/// </summary>
public sealed record CalendarEventInfo(
    string Subject,
    DateTime StartLocal,
    DateTime EndLocal,
    string? Location,
    bool IsOnline,
    string? OrganizerName,
    string ProviderKey = "",
    string AccountKey = "",
    string EventId = "");

/// <summary>
/// Outcome of reading the current session user's calendar for a week. <see cref="HasConnector"/> is false
/// when no connector exists (UI shows the connect hint); <see cref="Notice"/> carries a short provider
/// failure message when the read degraded (UI shows it instead of events). Never throws.
/// </summary>
public sealed record CalendarReadResult(
    bool HasConnector,
    IReadOnlyList<CalendarEventInfo> Events,
    string? Notice);

/// <summary>
/// Reads calendar events for the current session user over a local date range (inclusive). Implementations
/// resolve the user's connector internally and degrade gracefully (empty list + notice) on provider errors.
/// </summary>
public interface ICalendarEventReader
{
    Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct);
}
