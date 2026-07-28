using RazorSlicesHtmx.Demo.Features.About.Slices;

namespace RazorSlicesHtmx.Demo.Features.About;

public sealed class AboutEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly AboutContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("about", "About", "", Order: 100)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", () =>
            Result.Page(_AboutDetail.Create(_content.CreateDetailModel())));
    }
}