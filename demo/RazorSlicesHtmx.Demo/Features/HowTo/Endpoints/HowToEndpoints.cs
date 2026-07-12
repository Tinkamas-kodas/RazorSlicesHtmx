using RazorSlicesHtmx.Demo.Features.HowTo.Services;
using RazorSlicesHtmx.Demo.Features.HowTo.Slices;

namespace RazorSlicesHtmx.Demo.Features.HowTo.Endpoints;

public sealed class HowToEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly HowToContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("howto", "HowTo", "/howto", Order: 400)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/howto", () =>
            Result.For(_HowToPage.Create()).BuildAsync());

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