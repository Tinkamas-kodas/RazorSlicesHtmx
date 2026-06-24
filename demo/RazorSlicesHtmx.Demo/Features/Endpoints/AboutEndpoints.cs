using RazorSlicesHtmx.Demo.Features.About.Services;

namespace RazorSlicesHtmx.Demo.Features.About.Endpoints;

public sealed class AboutEndpoints : IFeatureModule
{
    private readonly AboutContentService _content = new();

    public PageDefinition Page => _content.CreatePageDefinition();

    public void MapEndpoints(WebApplication app)
    {
    }
}