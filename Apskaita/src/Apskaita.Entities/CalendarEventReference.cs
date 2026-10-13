namespace Apskaita.Entities;

/// <summary>
/// Stable identity of an external calendar event, stored as an EF owned type inside
/// <c>time_trackings</c> (same table). The triple (provider, account, external id) uniquely identifies
/// an event across providers and mailboxes; a <see cref="TimeTracking"/> row either carries a full
/// reference (created from a calendar event) or none at all (manual entry). Uniqueness of the triple
/// is enforced by a database index, so at most one entry ever references a given event.
/// </summary>
public sealed record CalendarEventReference(CalendarProvider CalendarProvider, string AccountKey, string ExternalEventId)
{
    /// <summary>Max length of <see cref="AccountKey"/> and <see cref="ExternalEventId"/> (DB and validation cap).</summary>
    public const int MaxKeyLength = 255;

    /// <summary>Lower-cased provider key for URL/query comparison (e.g. "outlook365").</summary>
    public string ProviderKey => CalendarProvider.ToString().ToLowerInvariant();

    /// <summary>
    /// Validates raw reference parts and builds the value object. False when the provider key does not
    /// parse (case-insensitive), any part is blank, or a key exceeds <see cref="MaxKeyLength"/>.
    /// </summary>
    public static bool TryCreate(string? providerKey, string? accountKey, string? externalEventId, out CalendarEventReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(externalEventId))
        {
            return false;
        }

        if (!Enum.TryParse<CalendarProvider>(providerKey.Trim(), ignoreCase: true, out var provider))
        {
            return false;
        }

        if (accountKey.Length > MaxKeyLength || externalEventId.Length > MaxKeyLength)
        {
            return false;
        }

        reference = new CalendarEventReference(provider, accountKey.Trim(), externalEventId.Trim());
        return true;
    }
}
