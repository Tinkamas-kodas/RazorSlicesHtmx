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
    private readonly NavigationItem[] _routeItems;
    private readonly Dictionary<string, int> _keyToFeatureIndex;

    private FeatureRegistry(IFeatureModule[] features)
    {
        _features = features;
        _navigation = features
            .SelectMany(f => f.NavigationItems)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _routeItems = CollectRouteItems(_navigation).ToArray();
        _keyToFeatureIndex = BuildKeyToFeatureIndex(features);
    }

    public NavigationItem DefaultRouteItem =>
        _routeItems.Length > 0
            ? _routeItems[0]
            : throw new InvalidOperationException("No navigable route items are registered.");

    public IReadOnlyList<NavigationItem> RouteItems => _routeItems;

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

    public bool TryGetRouteItem(string? key, out NavigationItem routeItem)
    {
        routeItem = _routeItems.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) ?? DefaultRouteItem;

        return _routeItems.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<FeatureShellContext> CreateShellContextAsync(
        NavigationItem currentItem,
        RazorSlices.RazorSlice detail,
        ClaimsPrincipal? user,
        IAuthorizationService? authService,
        string? pageTitle = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = await FilterNavigationAsync(_navigation, user, authService, cancellationToken);
        return new FeatureShellContext(currentItem, filtered, detail, pageTitle ?? currentItem.Label);
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

    private static IEnumerable<NavigationItem> CollectRouteItems(IReadOnlyList<NavigationItem> items)
    {
        foreach (var item in items)
        {
            if (item is NavigationGroupItem group)
            {
                foreach (var child in CollectRouteItems(group.Children))
                {
                    yield return child;
                }
            }
            else
            {
                yield return item;
            }
        }
    }

    private static Dictionary<string, int> BuildKeyToFeatureIndex(IFeatureModule[] features)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < features.Length; i++)
        {
            foreach (var item in CollectRouteItems(features[i].NavigationItems))
            {
                map.TryAdd(item.Key, i);
            }
        }
        return map;
    }

    /// <summary>Returns the feature index for the given nav item, or -1 if not found.</summary>
    public int GetFeatureIndex(NavigationItem item) =>
        _keyToFeatureIndex.TryGetValue(item.Key, out var idx) ? idx : -1;

    /// <summary>
    /// Resolves the feature index for the feature whose route prefix matches <paramref name="currentUrl"/>.
    /// Returns -1 when <paramref name="currentUrl"/> is null/empty or no match is found.
    /// </summary>
    public int FindActiveFeatureIndex(string? currentUrl)
    {
        var path = ExtractPath(currentUrl);
        if (string.IsNullOrEmpty(path)) return -1;

        foreach (var item in _routeItems)
        {
            var prefix = GetFirstPathSegmentPrefix(item.Route);
            if (prefix is not null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return GetFeatureIndex(item);
            }
        }

        return -1;
    }

    private static string? ExtractPath(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs)) return abs.AbsolutePath;
        var q = url.IndexOf('?');
        return q >= 0 ? url[..q] : url;
    }

    private static string? GetFirstPathSegmentPrefix(string route)
    {
        // "/items/list" → "/items/"   "/about" → "/about"
        var trimmed = route.TrimStart('/');
        if (string.IsNullOrEmpty(trimmed)) return null;
        var slash = trimmed.IndexOf('/');
        var segment = slash >= 0 ? trimmed[..slash] : trimmed;
        return string.IsNullOrEmpty(segment) ? null : $"/{segment}";
    }
}