namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// A non-navigable group node that contains child <see cref="NavigationItem"/> entries.
/// Use this to represent collapsible menu sections or category headers in the navigation tree.
/// </summary>
public sealed record NavigationGroupItem(
    string Key,
    string Label,
    IReadOnlyList<NavigationItem> Children,
    int Order = 100,
    string? AuthorizationPolicy = null)
    : NavigationItem(Key, Label, Order, AuthorizationPolicy);
