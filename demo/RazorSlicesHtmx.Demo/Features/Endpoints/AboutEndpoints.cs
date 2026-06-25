using RazorSlicesHtmx.Demo.Features.About.Services;

namespace RazorSlicesHtmx.Demo.Features.About.Endpoints;

public sealed class AboutEndpoints : IFeatureModule
{
    private readonly AboutContentService _content = new();

    public IReadOnlyList<NavigationItem> NavigationItems => [_content.CreateNavigationItem()];

    public void MapEndpoints(WebApplication app)
    {
    }
}