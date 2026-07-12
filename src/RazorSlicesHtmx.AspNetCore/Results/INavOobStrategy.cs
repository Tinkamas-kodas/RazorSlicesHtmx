using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Results;

/// <summary>
/// Determines whether the navigation OOB slice should be included in an HTMX response.
/// Register a custom implementation via DI to override the default behaviour.
/// </summary>
public interface INavOobStrategy
{
    /// <summary>
    /// Returns <c>true</c> if the navigation OOB slice should be sent with this response.
    /// </summary>
    bool ShouldSendNav(HttpRequest request, NavigationItem routeItem);
}
