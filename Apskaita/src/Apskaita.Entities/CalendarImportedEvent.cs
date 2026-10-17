namespace Apskaita.Entities;

/// <summary>
/// A single parsed <c>VEVENT</c> belonging to an imported (<c>Ics</c>) calendar connector. The raw
/// <c>.ics</c> text is never persisted — only these normalized rows are stored.
/// <para>
/// Uploading a file replaces the rows of its connector in one transaction; the connector itself (its name,
/// which is the account key of the event references) survives every import. Removing the connector
/// (disconnect) cascades to these rows. The time-tracking panel reads them exactly like Graph events, so an
/// imported event is clickable and can create a time entry the same way.
/// </para>
/// </summary>
public class CalendarImportedEvent : BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>The imported calendar this event belongs to. Deleting the connector cascades to this row.</summary>
    public Guid ConnectorId { get; set; }

    /// <summary>
    /// The imported calendar. Deleting the connector cascades to this row.
    /// </summary>
    public CalendarConnector Connector { get; set; } = null!;

    /// <summary>
    /// Identity of this occurrence inside its connector (REQ-018): the RFC 5545 <c>UID</c> for a single
    /// event, or <c>&lt;UID&gt;@&lt;utc start&gt;</c> for one occurrence of a recurring series. A series shares
    /// one <c>UID</c>, so the raw <c>UID</c> cannot identify a single occurrence — and the time-entry link
    /// is per occurrence.
    /// </summary>
    public string ExternalEventId { get; set; } = string.Empty;

    /// <summary>
    /// The RFC 5545 <c>UID</c> of the series this occurrence was expanded from; null for a single (not
    /// recurring) event. Rows of one series share it, which is what "same appointment, other date" means.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>Event start as UTC; all-day events use midnight UTC of the display date.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Event end as UTC (nullable for all-day events, which are date-only).</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>Event subject / summary (REQ-023: untrusted, encoded on render).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Event location (REQ-023: untrusted, encoded on render).</summary>
    public string? Location { get; set; }

    /// <summary>Organizer display name (REQ-023: untrusted, encoded on render).</summary>
    public string? OrganizerName { get; set; }

    /// <summary>
    /// When this row was written by an import (UTC). Every import stamps the whole snapshot with the same
    /// value, so it distinguishes the current snapshot from a replaced one.
    /// </summary>
    public DateTime ImportedAtUtc { get; set; }

    /// <summary>
    /// Imported events carry no reliable online-meeting flag; always false. Kept on the entity so the
    /// panel renders the same "Online" hint as Graph events when a provider sets it.
    /// </summary>
    public bool IsOnline { get; set; }
}
