namespace RazorSlicesHtmx.AspNetCore.Features;

public static class FeaturePageEndpointRouteBuilderExtensions
{
    public static void MapFeaturePages(this WebApplication app, FeatureRegistry features)
    {
        app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Redirect(features.DefaultRouteItem.Route));
    }
}