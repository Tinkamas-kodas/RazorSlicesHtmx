using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Features;

public sealed class FeatureRegistry
{
    private readonly IFeatureModule[] _features;
    private readonly PageDefinition[] _pages;

    private FeatureRegistry(IFeatureModule[] features)
    {
        _features = features;
        _pages = features
            .Select(feature => feature.Page)
            .OrderBy(page => page.Metadata.NavigationOrder)
            .ThenBy(page => page.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public PageDefinition DefaultPage => _pages[0];

    public static FeatureRegistry Discover(Assembly assembly, IServiceProvider services)
    {
        var features = assembly
            .GetTypes()
            .Where(type => typeof(IFeatureModule).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract && !type.IsInterface)
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

    public bool TryGetPage(string? key, out PageDefinition page)
    {
        page = _pages.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) ?? DefaultPage;

        return _pages.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public FeatureShellContext CreateShellContext(PageDefinition currentPage, RazorSlices.RazorSlice detail)
    {
        var pages = _pages
            .Select(item => item.Metadata)
            .ToArray();

        return new FeatureShellContext(currentPage.Metadata, pages, detail);
    }
}