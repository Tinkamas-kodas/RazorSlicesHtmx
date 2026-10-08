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
    private readonly string[] _featureRouteGroups;

    private FeatureRegistry((IFeatureModule Module, string RouteGroupPrefix)[] featureDefinitions)
    {
        _features = featureDefinitions.Select(x => x.Module).ToArray();
        _featureRouteGroups = featureDefinitions.Select(x => NormalizeRouteGroupPrefix(x.RouteGroupPrefix)).ToArray();
        _navigation = featureDefinitions
            .SelectMany(x => ApplyRouteGroupPrefix(x.Module.NavigationItems, NormalizeRouteGroupPrefix(x.RouteGroupPrefix)))
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _routeItems = CollectRouteItems(_navigation).ToArray();
        _keyToFeatureIndex = BuildKeyToFeatureIndex(_features);
    }

    public NavigationItem DefaultRouteItem =>
        _routeItems.Length > 0
            ? _routeItems[0]
            : throw new InvalidOperationException("No navigable route items are registered.");

    public IReadOnlyList<NavigationItem> RouteItems => _routeItems;

    

    public static FeatureRegistry FromRegistrations(IServiceProvider services)
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<FeatureRegistrationOptions>>().Value;
        if (options.Registrations.Count == 0)
        {
            throw new InvalidOperationException(
                "No features were registered. Register features explicitly with services.AddFeature<TFeature>(...).");
        }

        var features = options.Registrations.Select(registration =>
        {
            if (!typeof(IFeatureModule).IsAssignableFrom(registration.FeatureType))
            {
                throw new InvalidOperationException(
                    $"Type '{registration.FeatureType.FullName}' must implement {nameof(IFeatureModule)}.");
            }

            var module = (IFeatureModule)ActivatorUtilities.CreateInstance(services, registration.FeatureType);
            var routeGroupPrefix = string.IsNullOrWhiteSpace(registration.RouteGroupPrefix)
                ? InferRouteGroupPrefix(module)
                : registration.RouteGroupPrefix!;

            return (module, routeGroupPrefix);
        }).ToArray();

        return new FeatureRegistry(features);
    }

    public void MapEndpoints(WebApplication app)
    {
        for (var i = 0; i < _features.Length; i++)
        {
            var group = app.MapGroup(_featureRouteGroups[i]);
            _features[i].MapEndpoints(group);
        }
    }

    public bool TryGetRouteItem(string? key, out NavigationItem routeItem)
    {
        routeItem = _routeItems.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) ?? DefaultRouteItem;

        return _routeItems.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public FeatureShellContext CreateShellContext(
        NavigationItem currentItem,
        RazorSlices.RazorSlice detail,
        ClaimsPrincipal? user,
        IAuthorizationService? authService,
        string? pageTitle = null)
    {
        var filtered = FilterNavigation(_navigation, user, authService);
        return new FeatureShellContext(currentItem, filtered, detail, pageTitle ?? currentItem.Label);
    }

    private static IReadOnlyList<NavigationItem> FilterNavigation(
        IReadOnlyList<NavigationItem> items,
        ClaimsPrincipal? user,
        IAuthorizationService? authService)
    {
        if (user is null || authService is null)
        {
            return items;
        }

        var result = new List<NavigationItem>();

        foreach (var item in items)
        {
            if (item.AuthorizationPolicy is not null)
            {
                var authResult = authService.AuthorizeAsync(user, item.AuthorizationPolicy).GetAwaiter().GetResult();
                if (!authResult.Succeeded) continue;
            }

            if (item is NavigationGroupItem group)
            {
                var filteredChildren = FilterNavigation(group.Children, user, authService);
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

    private static IReadOnlyList<NavigationItem> ApplyRouteGroupPrefix(IReadOnlyList<NavigationItem> items, string routeGroupPrefix)
    {
        return items.Select(item => ApplyRouteGroupPrefix(item, routeGroupPrefix)).ToArray();
    }

    private static NavigationItem ApplyRouteGroupPrefix(NavigationItem item, string routeGroupPrefix)
    {
        if (item is NavigationGroupItem group)
        {
            return group with { Children = ApplyRouteGroupPrefix(group.Children, routeGroupPrefix) };
        }

        var route = CombineRoute(routeGroupPrefix, item.Route);
        return item with { Route = route };
    }

    private static string CombineRoute(string routeGroupPrefix, string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return routeGroupPrefix;
        }

        if (Uri.TryCreate(route, UriKind.Absolute, out _))
        {
            return route;
        }

        // Backward compatibility: keep explicit absolute app routes unchanged.
        if (route.StartsWith('/'))
        {
            return route;
        }

        return routeGroupPrefix == "/"
            ? "/" + route.TrimStart('/')
            : $"{routeGroupPrefix}/{route.TrimStart('/')}";
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

        var activeIndex = -1;
        var bestMatchLength = -1;

        for (var i = 0; i < _featureRouteGroups.Length; i++)
        {
            var prefix = _featureRouteGroups[i];
            if (!PathMatchesPrefix(path, prefix))
            {
                continue;
            }

            if (prefix.Length <= bestMatchLength)
            {
                continue;
            }

            activeIndex = i;
            bestMatchLength = prefix.Length;
        }

        return activeIndex;
    }

    private static string? ExtractPath(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs)) return abs.AbsolutePath;
        var q = url.IndexOf('?');
        return q >= 0 ? url[..q] : url;
    }

    private static string NormalizeRouteGroupPrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "/";
        }

        var normalized = ExtractPath(prefix) ?? "/";
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static bool PathMatchesPrefix(string path, string prefix)
    {
        if (prefix == "/")
        {
            return true;
        }

        var normalizedPath = NormalizeRouteGroupPrefix(path);
        return normalizedPath.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferRouteGroupPrefix(IFeatureModule feature)
    {
        var routes = CollectRouteItems(feature.NavigationItems)
            .Select(x => ExtractPath(x.Route))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => NormalizeRouteGroupPrefix(x!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (routes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Feature '{feature.GetType().FullName}' does not define navigable routes.");
        }

        if (routes.Length == 1)
        {
            return routes[0];
        }

        var commonSegments = routes
            .Select(route => route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        var maxLength = commonSegments.Min(parts => parts.Length);
        var take = 0;

        for (var i = 0; i < maxLength; i++)
        {
            var segment = commonSegments[0][i];
            if (commonSegments.All(parts => string.Equals(parts[i], segment, StringComparison.OrdinalIgnoreCase)))
            {
                take++;
                continue;
            }

            break;
        }

        if (take == 0)
        {
            return routes[0];
        }

        return "/" + string.Join('/', commonSegments[0].Take(take));
    }
}