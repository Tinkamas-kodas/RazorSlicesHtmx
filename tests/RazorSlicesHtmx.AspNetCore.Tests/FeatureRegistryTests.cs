using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class FeatureRegistryTests
{
    [Fact]
    public async Task Discover_orders_pages_and_builds_navigation_layout()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var registry = FeatureRegistry.Discover(typeof(FeatureRegistryTests).Assembly, services);

        var shellContext = await registry.CreateShellContextAsync(registry.DefaultRouteItem, null!, null, null);

        Assert.Equal("alpha", registry.DefaultRouteItem.Key);
        Assert.Equal("alpha", shellContext.CurrentItem.Key);
        Assert.Equal("Alpha", shellContext.CurrentItem.Label);
        Assert.Collection(
            shellContext.Navigation.OfType<NavigationRouteItem>(),
            item => Assert.Equal("alpha", item.Key),
            item => Assert.Equal("bravo", item.Key),
            item => Assert.Equal("charlie", item.Key));
    }

    [Fact]
    public void TryGetRouteItem_returns_default_for_unknown_key()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var registry = FeatureRegistry.Discover(typeof(FeatureRegistryTests).Assembly, services);

        var found = registry.TryGetRouteItem("missing", out var routeItem);

        Assert.False(found);
        Assert.Equal(registry.DefaultRouteItem, routeItem);
    }

    [Fact]
    public async Task Discover_multi_assembly_merges_features()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var assemblies = new[] { typeof(FeatureRegistryTests).Assembly };

        var registry = FeatureRegistry.Discover(assemblies, services);

        var shellContext = await registry.CreateShellContextAsync(registry.DefaultRouteItem, null!, null, null);
        Assert.Equal(3, shellContext.Navigation.Count);
    }

    [Fact]
    public async Task Discover_multi_assembly_deduplicates_same_assembly()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var assembly = typeof(FeatureRegistryTests).Assembly;
        var assemblies = new[] { assembly, assembly };

        var registry = FeatureRegistry.Discover(assemblies, services);

        var shellContext = await registry.CreateShellContextAsync(registry.DefaultRouteItem, null!, null, null);
        Assert.Equal(3, shellContext.Navigation.Count);
    }

    [Fact]
    public void Discover_multi_assembly_throws_when_no_features()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var assemblies = new[] { typeof(string).Assembly };

        Assert.Throws<InvalidOperationException>(() =>
            FeatureRegistry.Discover(assemblies, services));
    }
}

public sealed class AlphaFeature : IFeatureModule
{
    public IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new NavigationRouteItem("alpha", "Alpha", "/alpha", new PageDefinition(() => null!))
    ];

    public void MapEndpoints(WebApplication app)
    {
    }
}

public sealed class BravoFeature : IFeatureModule
{
    public IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new NavigationRouteItem("bravo", "Bravo", "/bravo", new PageDefinition(() => null!))
    ];

    public void MapEndpoints(WebApplication app)
    {
    }
}

public sealed class CharlieFeature : IFeatureModule
{
    public IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new NavigationRouteItem("charlie", "Charlie", "/charlie", new PageDefinition(() => null!), Order: 200)
    ];

    public void MapEndpoints(WebApplication app)
    {
    }
}
