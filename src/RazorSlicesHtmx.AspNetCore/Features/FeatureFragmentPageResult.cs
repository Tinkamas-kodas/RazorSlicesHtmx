using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public static class FeatureFragmentPageResult
{
    public static IResult Create(
        HttpRequest request,
        FeatureRegistry features,
        PageDefinition page,
        RazorSlice pageDetail,
        RazorSlice htmxFragment)
    {
        return request.IsHtmxRequest()
            ? htmxFragment
            : SliceResults.SlicePage(request, features, page, pageDetail);
    }
}