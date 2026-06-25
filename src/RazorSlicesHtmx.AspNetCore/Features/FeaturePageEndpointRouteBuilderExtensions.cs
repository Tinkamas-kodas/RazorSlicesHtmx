using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public static class FeaturePageEndpointRouteBuilderExtensions
{
    public static void MapFeaturePages(this WebApplication app, FeatureRegistry features)
    {
        app.MapGet("/", (HttpRequest request) =>
            BuildPageResultAsync(request, features, features.DefaultRouteItem));

        foreach (var routeItem in features.RouteItems)
        {
            app.MapGet(routeItem.Route, (HttpRequest request) =>
                BuildPageResultAsync(request, features, routeItem));
        }
    }

    private static Task<IResult> BuildPageResultAsync(
        HttpRequest request,
        FeatureRegistry features,
        NavigationRouteItem routeItem)
    {
        return SliceResults.SlicePageAsync(
            request,
            features,
            routeItem,
            routeItem.Page.CreateDetail);
    }
}