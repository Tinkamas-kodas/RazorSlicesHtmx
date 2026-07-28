using RazorSlicesHtmx.Demo.Features.HowTo.Services;
using RazorSlicesHtmx.Demo.Features.HowTo.Slices;

namespace RazorSlicesHtmx.Demo.Features.HowTo.Endpoints;

public sealed class HowToEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly HowToContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("howto", "HowTo", "", Order: 400)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", () =>
            Result.Page(_HowToPage.Create()));

        group.MapGet("step/{key}", (string key) =>
        {
            if (!_content.TryCreateStepSlice(key, out var stepSlice))
            {
                return Results.NotFound();
            }

            return HtmxFragmentResult
                .Create(stepSlice)
                .WithOob("#howto-pills", _HowToPillsBody.Create());
        });
    }
}