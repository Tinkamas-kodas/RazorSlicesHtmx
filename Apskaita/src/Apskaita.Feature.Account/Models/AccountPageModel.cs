namespace Apskaita.Feature.Account.Models;

/// <summary>
/// View model for the account page: one card per known calendar provider plus an optional notice from the
/// connect round-trip (<c>?connected=</c> / <c>?connectError=</c> query flags).
/// </summary>
public sealed record AccountPageModel(
    string? Notice,
    string NoticeKind,
    IReadOnlyList<CalendarConnectorCardModel> Connectors,
    string? AdminConsentUrl);

/// <summary>
/// A single calendar provider card. <see cref="IsImplemented"/> = false renders a "coming soon" placeholder
/// (REQ-009); otherwise the card lists every connected account (<see cref="Connections"/>) with its sync
/// metadata, plus a Connect action for adding another account (REQ-001).
/// </summary>
public sealed record CalendarConnectorCardModel(
    string Key,
    string Label,
    bool IsImplemented,
    IReadOnlyList<CalendarConnectionModel> Connections);

/// <summary>One connected account of a provider, shown as a row inside the provider card.</summary>
public sealed record CalendarConnectionModel(
    Guid Id,
    string AccountEmail,
    string DisplayName,
    DateTime ConnectedAtUtc,
    DateTime? LastSyncAtUtc,
    string? LastSyncError);
