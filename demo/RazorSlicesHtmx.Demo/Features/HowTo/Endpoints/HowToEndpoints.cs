using RazorSlicesHtmx.Demo.Features.HowTo.Services;
using RazorSlicesHtmx.Demo.Features.HowTo.Slices;

namespace RazorSlicesHtmx.Demo.Features.HowTo.Endpoints;

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