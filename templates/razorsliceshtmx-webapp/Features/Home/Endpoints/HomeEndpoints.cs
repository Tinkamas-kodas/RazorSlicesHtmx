using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using RshtmxApp.Features.Home.Services;
using RshtmxApp.Features.Home.Slices;

namespace RshtmxApp.Features.Home.Endpoints;

public sealed class HomeEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly HomeContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("home", "Home", "", Order: 0)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", () => Result.Page(_HomePage.Create()));
    }
}
