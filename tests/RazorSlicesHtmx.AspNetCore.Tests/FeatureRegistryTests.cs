using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class FeatureRegistryTests
{
    [Fact]
    public void Discover_orders_pages_and_builds_navigation_layout()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var registry = FeatureRegistry.Discover(typeof(FeatureRegistryTests).Assembly, services);

        var shellContext = registry.CreateShellContext(registry.DefaultPage, null!);

        Assert.Equal("alpha", registry.DefaultPage.Key);
        Assert.Equal("alpha", shellContext.CurrentPage.Key);
        Assert.Equal("Alpha", shellContext.CurrentPage.Label);
        Assert.Collection(
            shellContext.Pages,
            item => Assert.Equal("alpha", item.Key),
            item => Assert.Equal("bravo", item.Key),
            item => Assert.Equal("charlie", item.Key));
    }

    [Fact]
    public void TryGetPage_returns_default_page_for_unknown_key()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var registry = FeatureRegistry.Discover(typeof(FeatureRegistryTests).Assembly, services);

        var found = registry.TryGetPage("missing", out var page);

        Assert.False(found);
        Assert.Equal(registry.DefaultPage, page);
    }

    [Fact]
    public void Discover_multi_assembly_merges_features()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var assemblies = new[] { typeof(FeatureRegistryTests).Assembly };

        var registry = FeatureRegistry.Discover(assemblies, services);

        var shellContext = registry.CreateShellContext(registry.DefaultPage, null!);
        Assert.Equal(3, shellContext.Pages.Count);
    }

    [Fact]
    public void Discover_multi_assembly_deduplicates_same_assembly()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var assembly = typeof(FeatureRegistryTests).Assembly;
        var assemblies = new[] { assembly, assembly };

        var registry = FeatureRegistry.Discover(assemblies, services);

        var shellContext = registry.CreateShellContext(registry.DefaultPage, null!);
        Assert.Equal(3, shellContext.Pages.Count);
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
    public PageDefinition Page => new(
        new FeatureMetadata(100, "alpha", "Alpha", "/alpha", "First"),
        () => null!);

    public void MapEndpoints(WebApplication app)
    {
    }
}

public sealed class BravoFeature : IFeatureModule
{
    public PageDefinition Page => new(
        new FeatureMetadata(100, "bravo", "Bravo", "/bravo", "Second"),
        () => null!);

    public void MapEndpoints(WebApplication app)
    {
    }
}

public sealed class CharlieFeature : IFeatureModule
{
    public PageDefinition Page => new(
        new FeatureMetadata(200, "charlie", "Charlie", "/charlie", "Third"),
        () => null!);

    public void MapEndpoints(WebApplication app)
    {
    }
}
