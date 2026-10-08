using Microsoft.AspNetCore.Mvc;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.Demo.Features.Lookup.Models;
using RazorSlicesHtmx.Demo.Features.Lookup.Services;
using RazorSlicesHtmx.Demo.Features.Lookup.Slices;
using RazorSlicesHtmx.Demo.Shared.Routing;

namespace RazorSlicesHtmx.Demo.Features.Lookup.Endpoints;

public sealed class LookupEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly LookupContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("lookup", "Lookup", "", Order: 250)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", () =>
            Result.Page(LookupPage.Create(_content.CreatePageModel())));

        group.MapGet("search", (IFeatureUri featureUri, [AsParameters] LookupRequest request) =>
        {
            request.Path = string.IsNullOrWhiteSpace(request.Path)
                ? featureUri.Uri("search")
                : request.Path;

            var model = _content.Search(request);
            return Result.Fragment(_LookupResults.Create(model));
        });

        group.MapGet("search-selected", (string value) =>
        {
            var item = _content.ResolveSelected(value);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("submit", ([FromForm] LookupDemoRequest request) =>
            Result.Fragment(_LookupSummary.Create(_content.BuildSummary(request))))
            .DisableAntiforgery();
    }
}
