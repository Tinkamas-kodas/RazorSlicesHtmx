using Apskaita.Entities;

namespace Apskaita.Shared.Calendar;

/// <summary>
/// Read-only cross-feature seam over the calendar connectors: answers whether the current session
/// user owns a connected account for a provider. Defined in <c>Apskaita.Shared</c> so a feature
/// that validates calendar event references (TimeTracking) can check ownership without reaching
/// into Account's internal <c>CalendarConnectorStore</c>.
/// <para>
/// The session user is resolved from the authentication principal inside the implementation —
/// callers pass only the reference parts.
/// </para>
/// </summary>
public interface ICalendarEventReferenceResolver
{
    /// <summary>True when the current user has a connected connector for <paramref name="provider"/> whose account key matches <paramref name="accountKey"/> (case-insensitive).</summary>
    Task<bool> OwnsAccountAsync(CalendarProvider provider, string accountKey, CancellationToken ct);
}
