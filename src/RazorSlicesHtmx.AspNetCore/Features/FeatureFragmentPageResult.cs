using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public static class FeatureFragmentPageResult
{
    public static async Task<IResult> CreateAsync(
        HttpRequest request,
        FeatureRegistry features,
        NavigationRouteItem routeItem,
        RazorSlice pageDetail,
        RazorSlice htmxFragment)
    {
        return request.IsHtmxRequest()
            ? htmxFragment
            : await SliceResults.SlicePageAsync(request, features, routeItem, pageDetail);
    }
}