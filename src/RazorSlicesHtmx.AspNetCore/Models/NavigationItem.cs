namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// A navigable leaf node in the navigation tree.
/// Use <see cref="NavigationGroupItem"/> for non-navigable group containers.
/// </summary>
public record NavigationItem(
    string Key,
    string Label,
    string Route,
    int Order = 100,
    string? AuthorizationPolicy = null);
