using RazorSlicesHtmx.Demo.Features.About.Services;
using RazorSlicesHtmx.Demo.Features.Slices;

namespace RazorSlicesHtmx.Demo.Features.About.Endpoints;

public sealed class AboutEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly AboutContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("about", "About", "/about", Order: 100)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/about", () =>
            Result.For(_AboutDetail.Create(_content.CreateDetailModel())).BuildAsync());
    }
}