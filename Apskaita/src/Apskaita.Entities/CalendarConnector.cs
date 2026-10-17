namespace Apskaita.Entities;

/// <summary>
/// Calendar integration provider key. Stored in MySQL as a native <c>enum</c> column via
/// <c>HasConversion&lt;string&gt;()</c>. <c>Outlook365</c> is the first implementation; further providers
/// (e.g. Google Calendar) are added here once their connector exists.
/// </summary>
public enum CalendarProvider
{
    Outlook365,
    Ics
}

/// <summary>
/// A user's connected calendar account. One row per (user, provider, account) — a user may connect several
/// mailboxes of the same provider (e.g. work and personal organizations). The row stores the OAuth refresh
/// token and the cached access token, both protected with ASP.NET Core Data Protection (never plaintext).
/// The time tracking page reads all of the current user's connectors to show calendar events. Deleting the
/// row (disconnect) forgets that account's tokens.
/// </summary>
public class CalendarConnector : BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>The owner of the connection; resolved from the session user, never from a request parameter.</summary>
    public Guid UserId { get; set; }

    /// <summary>The owning user. Deleting the user deletes their connectors (cascade).</summary>
    public User User { get; set; } = null!;

    public CalendarProvider Provider { get; set; }

    /// <summary>
    /// Stable per-account key within the provider (normalized account email). Together with
    /// (<see cref="UserId"/>, <see cref="Provider"/>) it is unique: reconnecting the same mailbox updates
    /// the row, connecting a different mailbox adds a new one.
    /// </summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>The connected account email (Graph <c>preferred_username</c>/<c>email</c>) shown in the UI.</summary>
    public string AccountEmail { get; set; } = string.Empty;

    /// <summary>Display name asserted by the provider for the connected account.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Data-Protection-protected OAuth refresh token (required for offline_access).</summary>
    public string RefreshTokenProtected { get; set; } = string.Empty;

    /// <summary>Data-Protection-protected cached Graph access token; null until the first fetch/refresh.</summary>
    public string? AccessTokenProtected { get; set; }

    /// <summary>UTC expiry of the cached access token; token is refreshed when null or within its lifetime.</summary>
    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    /// <summary>Space-separated delegated scopes granted at connect time (e.g. for re-consent detection).</summary>
    public string Scopes { get; set; } = string.Empty;

    public DateTime ConnectedAtUtc { get; set; }

    /// <summary>UTC time of the last successful token refresh + Graph fetch.</summary>
    public DateTime? LastSyncAtUtc { get; set; }

    /// <summary>Short message from the last failed sync (token refresh or Graph error); cleared on success.</summary>
    public string? LastSyncError { get; set; }
}
