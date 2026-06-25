namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// A navigable leaf node in the navigation tree. Binds a route to a <see cref="PageDefinition"/>
/// that provides the detail slice factory for full-page and feature-page rendering.
/// </summary>
public sealed record NavigationRouteItem(
    string Key,
    string Label,
    string Route,
    PageDefinition Page,
    int Order = 100,
    string? AuthorizationPolicy = null)
    : NavigationItem(Key, Label, Order, AuthorizationPolicy);
