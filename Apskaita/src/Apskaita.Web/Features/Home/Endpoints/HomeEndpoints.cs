using Apskaita.Web.Features.Home.Services;
using Apskaita.Web.Features.Home.Slices;
using Apskaita.Web.Shared;

namespace Apskaita.Web.Features.Home.Endpoints;

public sealed class HomeEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly HomeContentService _content = new();

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("MAIN","home", "Home", "", Order: 0)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", () => Result.Page(_HomePage.Create()));
    }
}
