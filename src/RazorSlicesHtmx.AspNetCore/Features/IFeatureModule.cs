using RazorSlicesHtmx.AspNetCore.Models;
using Microsoft.AspNetCore.Routing;

namespace RazorSlicesHtmx.AspNetCore.Features;

public interface IFeatureModule
{
    IReadOnlyList<NavigationItem> NavigationItems { get; }

    void MapEndpoints(RouteGroupBuilder group);
}