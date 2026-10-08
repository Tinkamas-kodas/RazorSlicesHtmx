using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class FeatureRegistryTests
{
    [Fact]
    public void FromRegistrations_orders_pages_and_builds_navigation_layout()
    {
        var services = CreateServices()
            .AddFeature<AlphaFeature>()
            .AddFeature<BravoFeature>()
            .AddFeature<CharlieFeature>()
            .BuildServiceProvider();
        var registry = FeatureRegistry.FromRegistrations(services);

        var shellContext = registry.CreateShellContext(registry.DefaultRouteItem, null!, null, null);

        Assert.Equal("alpha", registry.DefaultRouteItem.Key);
        Assert.Equal("alpha", shellContext.CurrentItem.Key);
        Assert.Equal("Alpha", shellContext.CurrentItem.Label);
        Assert.Collection(
            shellContext.Navigation,
            item => Assert.Equal("alpha", item.Key),
            item => Assert.Equal("bravo", item.Key),
            item => Assert.Equal("charlie", item.Key));
    }

    [Fact]
    public void TryGetRouteItem_returns_default_for_unknown_key()
    {
        var services = CreateServices()
            .AddFeature<AlphaFeature>()
            .AddFeature<BravoFeature>()
            .AddFeature<CharlieFeature>()
            .BuildServiceProvider();
        var registry = FeatureRegistry.FromRegistrations(services);

        var found = registry.TryGetRouteItem("missing", out var routeItem);

        Assert.False(found);
        Assert.Equal(registry.DefaultRouteItem, routeItem);
    }

    [Fact]
    public void FromRegistrations_throws_when_no_features()
    {
        var services = CreateServices().BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => FeatureRegistry.FromRegistrations(services));
    }

    [Fact]
    public void AddFeature_throws_when_feature_is_registered_twice()
    {
        var services = CreateServices()
            .AddFeature<AlphaFeature>();

        services.AddFeature<AlphaFeature>();
        var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => FeatureRegistry.FromRegistrations(provider));
    }

    [Fact]
    public void FromRegistrations_keeps_absolute_navigation_routes_unchanged()
    {
        var services = CreateServices()
            .AddFeature<AlphaFeature>("/catalog")
            .BuildServiceProvider();
        var registry = FeatureRegistry.FromRegistrations(services);

        Assert.Equal("/alpha", registry.DefaultRouteItem.Route);
    }

    private static IServiceCollection CreateServices()
    {
        return new ServiceCollection();
    }
}

public sealed class AlphaFeature : IFeatureModule
{
    public IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("alpha", "Alpha", "/alpha")
    ];

    public void MapEndpoints(RouteGroupBuilder group)
    {
    }
}

public sealed class BravoFeature : IFeatureModule
{
    public IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("bravo", "Bravo", "/bravo")
    ];

    public void MapEndpoints(RouteGroupBuilder group)
    {
    }
}

public sealed class CharlieFeature : IFeatureModule
{
    public IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("charlie", "Charlie", "/charlie", Order: 200)
    ];

    public void MapEndpoints(RouteGroupBuilder group)
    {
    }
}
