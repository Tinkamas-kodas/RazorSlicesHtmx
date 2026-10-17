using Apskaita.Shared.Calendar;

namespace Apskaita.Feature.Account.Models;

/// <summary>
/// Compact connection list for the time-tracking dock: providers with their connections and a health dot
/// per connection. Statistics are intentionally absent — the full table (with event counts and covered
/// ranges) belongs to the account page.
/// </summary>
public sealed record ConnectionsDockModel(
    // WeekStart is the display-zone Monday the dots were evaluated for, echoed into the dock's refresh URL.
    string WeekStart,
    IReadOnlyList<ConnectionsDockProviderModel> Providers);

/// <summary>One provider section of the dock (its Connect action plus the connected accounts).</summary>
public sealed record ConnectionsDockProviderModel(
    string Key,
    string Label,
    bool IsImplemented,
    bool IsImported,
    IReadOnlyList<ConnectionsDockConnectionModel> Connections);

/// <summary>
/// One connection row. <see cref="IsHealthy"/> drives the green/red dot: for an OAuth account it means the
/// last sync produced no error; for an imported calendar it means the stored events cover the displayed
/// week. <see cref="Tooltip"/> explains the dot.
/// </summary>
public sealed record ConnectionsDockConnectionModel(
    Guid Id,
    string Label,
    bool IsHealthy,
    string Tooltip,
    string ImportUrl);

/// <summary>
/// Maps the account page's connector cards to the dock's compact rows for a displayed week. Pure
/// presentation — the coverage decision compares stored UTC bounds against the week's UTC bounds.
/// </summary>
public static class ConnectionsDockModelFactory
{
    public static ConnectionsDockModel Create(
        IReadOnlyList<CalendarConnectorCardModel> cards,
        DateOnly anchor,
        DateTime weekStartUtc,
        DateTime weekEndUtc,
        Func<Guid, string> importUrl)
    {
        var providers = cards
            .Select(card =>
            {
                var isImported = card.Key.Equals(IcsCalendarImporter.ProviderKey, StringComparison.OrdinalIgnoreCase);

                var connections = card.Connections
                    .Select(c => CreateConnection(c, isImported, weekStartUtc, weekEndUtc, importUrl))
                    .ToList();

                return new ConnectionsDockProviderModel(card.Key, card.Label, card.IsImplemented, isImported, connections);
            })
            .ToList();

        return new ConnectionsDockModel(
            anchor.ToString("yyyy-MM-dd"),
            providers);
    }

    private static ConnectionsDockConnectionModel CreateConnection(
        CalendarConnectionModel connection,
        bool isImported,
        DateTime weekStartUtc,
        DateTime weekEndUtc,
        Func<Guid, string> importUrl)
    {
        var label = string.IsNullOrWhiteSpace(connection.DisplayName)
            ? connection.AccountEmail
            : connection.DisplayName;

        var url = importUrl(connection.Id);

        if (!isImported)
        {
            // OAuth: the dot reports the connector's own health (the last sync outcome).
            return new ConnectionsDockConnectionModel(
                connection.Id,
                label,
                string.IsNullOrEmpty(connection.LastSyncError),
                connection.LastSyncError ?? "Connected",
                url);
        }

        // Imported calendar: the dot reports whether it can serve the displayed week at all.
        var stats = connection.Stats ?? CalendarConnectionStats.Empty;
        if (stats.EventCount == 0)
        {
            return new ConnectionsDockConnectionModel(connection.Id, label, false, "No events imported yet.", url);
        }

        var first = stats.FirstStartUtc ?? stats.LastEndUtc ?? weekStartUtc;
        var last = stats.LastEndUtc ?? stats.FirstStartUtc ?? weekStartUtc;
        var coversWeek = first <= weekEndUtc && last >= weekStartUtc;

        var range = $"Events {first.ToLocalTime():yyyy-MM-dd} – {last.ToLocalTime():yyyy-MM-dd}";
        var tooltip = coversWeek
            ? $"{range} ({stats.EventCount} event(s))."
            : $"{range} — nothing in this week.";

        return new ConnectionsDockConnectionModel(connection.Id, label, coversWeek, tooltip, url);
    }
}
