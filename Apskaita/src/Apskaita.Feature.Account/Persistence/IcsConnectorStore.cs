using System.Text.RegularExpressions;
using Apskaita.Feature.Account.Models;
using Apskaita.Shared.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Account.Persistence;

/// <summary>
/// Write store for imported (.ics) calendars (feature-calendar-connectors-1 §4.3, revised 2026-10-16):
/// a connector is a NAMED container the user creates once, and an import replaces only its event rows —
/// the connector itself survives every upload. The owner always comes from the session principal
/// (SEC-002), never from a route or form value.
/// </summary>
internal sealed class IcsConnectorStore(IAccountStore store, CalendarConnectorStore connectors)
{
    /// <summary>Scopes marker on the connector row — an imported calendar has no OAuth tokens.</summary>
    public const string ImportScopes = "ics-file-import";

    /// <summary>Allowed name characters: letters (incl. Lithuanian), digits, space, dot, dash, underscore.</summary>
    private static readonly Regex NamePattern = new("^[\\p{L}\\p{N} ._-]+$", RegexOptions.Compiled);

    /// <summary>The current user's imported calendars, ordered by name.</summary>
    public Task<IReadOnlyList<CalendarConnector>> GetOwnAsync(CancellationToken ct) =>
        connectors.GetOwnByProviderAsync(CalendarProvider.Ics, ct);

    /// <summary>One of the current user's imported calendars, or null when the id is not theirs.</summary>
    public async Task<CalendarConnector?> GetOwnByIdAsync(Guid id, CancellationToken ct)
    {
        var userId = connectors.CurrentUserId;
        if (userId is null)
        {
            return null;
        }

        return await store.Connectors
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value && c.Provider == CalendarProvider.Ics, ct);
    }

    /// <summary>
    /// Creates an empty named imported calendar. The name doubles as the account key of the event
    /// references (<c>CalendarEventReference</c>), so it must be unique per user and is never renamed.
    /// Returns the connector, or a user-presentable error.
    /// </summary>
    public async Task<(CalendarConnector? Connector, string? Error)> CreateOwnAsync(string? name, CancellationToken ct)
    {
        var userId = connectors.CurrentUserId;
        if (userId is null)
        {
            return (null, "Your session has expired. Sign in again.");
        }

        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return (null, "Enter a name for the imported calendar.");
        }

        if (trimmed.Length > FieldLengths.CalendarConnector.IcsName)
        {
            return (null, $"The name must be {FieldLengths.CalendarConnector.IcsName} characters or fewer.");
        }

        if (!NamePattern.IsMatch(trimmed))
        {
            return (null, "Use letters, digits, spaces, dot, dash or underscore in the name.");
        }

        var accountKey = trimmed.ToLowerInvariant();
        var taken = await store.Connectors.AnyAsync(
            c => c.UserId == userId.Value && c.Provider == CalendarProvider.Ics && c.AccountKey == accountKey, ct);

        if (taken)
        {
            return (null, $"An imported calendar named \"{trimmed}\" already exists.");
        }

        var now = DateTime.UtcNow;
        var connector = new CalendarConnector
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Provider = CalendarProvider.Ics,
            AccountKey = accountKey,
            AccountEmail = trimmed,
            DisplayName = trimmed,
            RefreshTokenProtected = string.Empty,
            Scopes = ImportScopes,
            ConnectedAtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = userId.Value,
            UpdatedAtUtc = now,
            UpdatedByUserId = userId.Value
        };

        store.Connectors.Add(connector);
        await store.SaveChangesAsync(ct);
        return (connector, null);
    }

    /// <summary>
    /// Replaces the snapshot of an owned imported calendar in one transaction: its stored event rows are
    /// removed and the freshly parsed ones inserted. The connector row survives — only its events change —
    /// and carries the import timestamp (<c>LastSyncAtUtc</c>).
    /// </summary>
    public async Task ImportAsync(CalendarConnector connector, IReadOnlyList<IcsParsedEvent> events, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var userId = connector.UserId;

        var stale = await store.ImportedEvents.Where(e => e.ConnectorId == connector.Id).ToListAsync(ct);
        store.ImportedEvents.RemoveRange(stale);

        store.ImportedEvents.AddRange(events.Select(evt => new CalendarImportedEvent
        {
            Id = Guid.NewGuid(),
            ConnectorId = connector.Id,
            ExternalEventId = evt.ExternalEventId,
            GroupId = evt.GroupId,
            StartUtc = evt.StartUtc,
            EndUtc = evt.EndUtc,
            Subject = evt.Subject,
            Location = evt.Location,
            OrganizerName = evt.OrganizerName,
            ImportedAtUtc = now,
            // An .ics export carries no reliable online-meeting flag.
            IsOnline = false,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
            UpdatedAtUtc = now,
            UpdatedByUserId = userId
        }));

        connector.ConnectedAtUtc = now;
        connector.LastSyncAtUtc = now;
        connector.LastSyncError = null;
        connector.UpdatedAtUtc = now;
        connector.UpdatedByUserId = userId;

        await store.SaveChangesAsync(ct);
    }

    /// <summary>Event count and covered range per connector; connectors without rows map to <see cref="CalendarConnectionStats.Empty"/>.</summary>
    public async Task<IReadOnlyDictionary<Guid, CalendarConnectionStats>> GetStatsAsync(IReadOnlyCollection<Guid> connectorIds, CancellationToken ct)
    {
        if (connectorIds.Count == 0)
        {
            return new Dictionary<Guid, CalendarConnectionStats>();
        }

        var rows = await store.ImportedEvents
            .Where(e => connectorIds.Contains(e.ConnectorId))
            .GroupBy(e => e.ConnectorId)
            .Select(g => new
            {
                ConnectorId = g.Key,
                Count = g.Count(),
                FirstStartUtc = g.Min(e => e.StartUtc),
                LastEndUtc = g.Max(e => e.EndUtc ?? e.StartUtc)
            })
            .ToListAsync(ct);

        // A calendar that was created but never filled has no group — report it as empty, not missing.
        var byId = connectorIds.Distinct().ToDictionary(id => id, _ => CalendarConnectionStats.Empty);
        foreach (var row in rows)
        {
            byId[row.ConnectorId] = new CalendarConnectionStats(row.Count, row.FirstStartUtc, row.LastEndUtc);
        }

        return byId;
    }
}
