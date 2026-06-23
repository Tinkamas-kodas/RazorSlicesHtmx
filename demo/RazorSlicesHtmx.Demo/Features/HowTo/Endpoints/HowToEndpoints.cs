using razr_slices_htmx2.Features.HowTo.Services;
using razr_slices_htmx2.Features.HowTo.Slices;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.HowTo.Endpoints;

public sealed class HowToEndpoints : IFeatureModule
{
    private readonly HowToContentService _content = new();

    public PageDefinition Page => _content.CreatePageDefinition();

    public void MapEndpoints(WebApplication app)
    {
        app.MapGet("/howto/step/{key}", (string key) =>
        {
            if (!_content.TryCreateStepSlice(key, out var stepSlice))
            {
                return Results.NotFound();
            }

            return HtmxFragmentResult
                .Create(stepSlice)
                .WithOob("#howto-pills", _HowToPillsBody.Create())
                .Build();
        });
    }
}