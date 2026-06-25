using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Features;

public interface IFeatureModule
{
    IReadOnlyList<NavigationItem> NavigationItems { get; }

    void MapEndpoints(WebApplication app);
}