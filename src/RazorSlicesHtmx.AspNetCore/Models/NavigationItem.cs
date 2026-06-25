namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Base type for navigation tree nodes. Derive from <see cref="NavigationGroupItem"/>
/// for non-navigable groups, or <see cref="NavigationRouteItem"/> for navigable pages.
/// </summary>
public abstract record NavigationItem(
    string Key,
    string Label,
    int Order = 100,
    string? AuthorizationPolicy = null);
