using razr_slices_htmx2.Features.About.Services;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.About.Endpoints;

public sealed class AboutEndpoints : IFeatureModule
{
    private readonly AboutContentService _content = new();

    public PageDefinition Page => _content.CreatePageDefinition();

    public void MapEndpoints(WebApplication app)
    {
    }
}