using Microsoft.Extensions.DependencyInjection;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Htmx;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Results;

/// <summary>
/// Default <see cref="INavOobStrategy"/> that compares the current browser URL
/// (<c>HX-Current-URL</c> header) against the requested route item to determine
/// whether the user is navigating to a different feature.
/// Navigation OOB is sent only when the active feature changes.
/// </summary>
public sealed class HxCurrentUrlNavStrategy(IServiceProvider services) : INavOobStrategy
{
    // Lazy to break the circular dependency: FeatureRegistry depends on feature modules
    // which depend on FeatureResultBuilder which depends on INavOobStrategy which would
    // otherwise depend on FeatureRegistry during construction.
    private FeatureRegistry? _registry;
    private FeatureRegistry Registry => _registry ??= services.GetRequiredService<FeatureRegistry>();

    public bool ShouldSendNav(HttpRequest request, NavigationItem routeItem)
    {
        var currentUrl = request.Headers[Hx.CurrentUrl].ToString();
        if (string.IsNullOrEmpty(currentUrl)) return true;

        var currentFeature = Registry.FindActiveFeatureIndex(currentUrl);
        var requestedFeature = Registry.GetFeatureIndex(routeItem);

        return currentFeature != requestedFeature;
    }
}
