using Apskaita.Entities;

namespace Apskaita.Shared.Calendar;

/// <summary>
/// The narrow persistence surface the Graph connector needs from the calendar-connector store: load the
/// current user's connectors for a provider, record a sync outcome, and commit. Defined in
/// <c>Apskaita.Shared</c> so the connector (which must not depend on <c>ApskaitaDbContext</c>) can be
/// satisfied by the store implementation that lives in the owning feature.
/// <para>
/// This is the seam that breaks the <c>Shared ↔ Account</c> cycle: <c>Outlook365Connector</c> depends on
/// this abstraction; <c>CalendarConnectorStore</c> (owned by Account) implements it.
/// </para>
/// </summary>
public interface ICalendarConnectorTokenStore
{
    /// <summary>All connected connectors of the current session user for one provider, ordered by account email.</summary>
    Task<IReadOnlyList<CalendarConnector>> GetOwnByProviderAsync(CalendarProvider provider, CancellationToken ct);

    /// <summary>
    /// Records the outcome of a sync attempt on a tracked connector row: success clears the error and stamps
    /// <c>LastSyncAtUtc</c>; failure stores a truncated message. Callers must follow with <see cref="SaveChangesAsync"/>.
    /// </summary>
    void ApplySyncResult(CalendarConnector connector, string? error);

    /// <summary>Persists token-cache and sync-metadata changes made by the Graph connector.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
