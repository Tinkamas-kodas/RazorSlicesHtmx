using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Features;

public sealed class FeatureRegistry
{
    private readonly IFeatureModule[] _features;
    private readonly IReadOnlyList<NavigationItem> _navigation;
    private readonly NavigationRouteItem[] _routeItems;

    private FeatureRegistry(IFeatureModule[] features)
    {
        _features = features;
        _navigation = features
            .SelectMany(f => f.NavigationItems)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _routeItems = CollectRouteItems(_navigation).ToArray();
    }

    public NavigationRouteItem DefaultRouteItem =>
        _routeItems.Length > 0
            ? _routeItems[0]
            : throw new InvalidOperationException("No navigable route items are registered.");

    public IReadOnlyList<NavigationRouteItem> RouteItems => _routeItems;

    public static FeatureRegistry Discover(Assembly assembly, IServiceProvider services) =>
        Discover([assembly], services);

    public static FeatureRegistry Discover(IEnumerable<Assembly> assemblies, IServiceProvider services)
    {
        var features = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(type => typeof(IFeatureModule).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Distinct()
            .Select(type => (IFeatureModule)ActivatorUtilities.CreateInstance(services, type))
            .ToArray();

        if (features.Length == 0)
        {
            throw new InvalidOperationException("No feature modules were discovered.");
        }

        return new FeatureRegistry(features);
    }

    public void MapEndpoints(WebApplication app)
    {
        foreach (var feature in _features)
        {
            feature.MapEndpoints(app);
        }
    }

    public bool TryGetRouteItem(string? key, out NavigationRouteItem routeItem)
    {
        routeItem = _routeItems.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) ?? DefaultRouteItem;

        return _routeItems.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<FeatureShellContext> CreateShellContextAsync(
        NavigationRouteItem currentItem,
        RazorSlices.RazorSlice detail,
        ClaimsPrincipal? user,
        IAuthorizationService? authService,
        CancellationToken cancellationToken = default)
    {
        var filtered = await FilterNavigationAsync(_navigation, user, authService, cancellationToken);
        return new FeatureShellContext(currentItem, filtered, detail);
    }

    private static async Task<IReadOnlyList<NavigationItem>> FilterNavigationAsync(
        IReadOnlyList<NavigationItem> items,
        ClaimsPrincipal? user,
        IAuthorizationService? authService,
        CancellationToken cancellationToken)
    {
        if (user is null || authService is null)
        {
            return items;
        }

        var result = new List<NavigationItem>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.AuthorizationPolicy is not null)
            {
                var authResult = await authService.AuthorizeAsync(user, item.AuthorizationPolicy);
                if (!authResult.Succeeded) continue;
            }

            if (item is NavigationGroupItem group)
            {
                var filteredChildren = await FilterNavigationAsync(group.Children, user, authService, cancellationToken);
                if (filteredChildren.Count > 0)
                {
                    result.Add(group with { Children = filteredChildren });
                }
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static IEnumerable<NavigationRouteItem> CollectRouteItems(IReadOnlyList<NavigationItem> items)
    {
        foreach (var item in items)
        {
            if (item is NavigationRouteItem route)
            {
                yield return route;
            }
            else if (item is NavigationGroupItem group)
            {
                foreach (var child in CollectRouteItems(group.Children))
                {
                    yield return child;
                }
            }
        }
    }
}