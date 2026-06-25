using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Minimal shell context passed to <see cref="Rendering.IFeaturePageRenderer"/>.
/// Contains the active navigation item, the filtered navigation tree, and the detail slice.
/// </summary>
public sealed record FeatureShellContext(
    NavigationRouteItem CurrentItem,
    IReadOnlyList<NavigationItem> Navigation,
    RazorSlice Detail);