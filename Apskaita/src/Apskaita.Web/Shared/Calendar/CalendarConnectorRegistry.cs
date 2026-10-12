using Apskaita.Entities;

namespace Apskaita.Web.Shared.Calendar;

/// <summary>
/// A calendar integration provider offered on the account page. Implementations with
/// <see cref="IsImplemented"/> = true provide connect/disconnect and event reading; placeholder providers
/// are rendered as "coming soon" so the registry shape is visible without code.
/// </summary>
public interface ICalendarProviderConnector
{
    /// <summary>Stable provider key, e.g. <c>outlook365</c>.</summary>
    string Key { get; }

    /// <summary>Display label for the provider card.</summary>
    string Label { get; }

    /// <summary>True when connect + event reading are implemented for this provider.</summary>
    bool IsImplemented { get; }

    /// <summary>
    /// Provider enum used for connector rows; null for placeholder providers that have no stored rows yet.
    /// </summary>
    CalendarProvider? Provider { get; }
}

/// <summary>
/// All known calendar providers, ordered for display. Resolved from DI so new connectors are picked up
/// automatically (REQ-009).
/// </summary>
public sealed class CalendarConnectorRegistry(IEnumerable<ICalendarProviderConnector> connectors)
{
    public IReadOnlyList<ICalendarProviderConnector> All { get; } = connectors
        .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public ICalendarProviderConnector? Find(string key) => All.FirstOrDefault(c =>
        string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Static provider metadata registered as a singleton (feature modules are singletons and must not
/// consume scoped connectors). <see cref="IsImplemented"/> = false renders a "coming soon" placeholder
/// (REQ-009); <see cref="Provider"/> is null for placeholders that have no stored rows yet.
/// </summary>
public sealed record CalendarProviderInfo(
    string Key,
    string Label,
    CalendarProvider? Provider,
    bool Implemented) : ICalendarProviderConnector
{
    public bool IsImplemented => Implemented;
}
