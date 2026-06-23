using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public static class FeaturePageEndpointRouteBuilderExtensions
{
    public static void MapFeaturePages(this WebApplication app, FeatureRegistry features)
    {
        app.MapGet("/", (HttpRequest request) => BuildPageResult(request, features, features.DefaultPage));

        app.MapGet("/{key}", (HttpRequest request, string key) =>
        {
            if (!features.TryGetPage(key, out var page))
            {
                return Microsoft.AspNetCore.Http.Results.NotFound();
            }

            return BuildPageResult(request, features, page);
        });
    }

    private static IResult BuildPageResult(HttpRequest request, FeatureRegistry features, PageDefinition page)
    {
        return SliceResults.SlicePage(
            request,
            features,
            page,
            page.CreateDetail);
    }
}