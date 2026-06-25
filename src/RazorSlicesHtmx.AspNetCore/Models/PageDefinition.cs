using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Binds a detail slice factory to a navigable route. Used by <see cref="NavigationRouteItem"/>
/// and the page rendering pipeline to create the detail content for a feature page.
/// </summary>
public sealed record PageDefinition(
    Func<HttpContext, RazorSlice> CreateDetail)
{
    public PageDefinition(Func<RazorSlice> createDetail)
        : this(_ => createDetail())
    {
    }
}