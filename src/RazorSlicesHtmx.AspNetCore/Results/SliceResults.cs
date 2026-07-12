using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Results;

public static class SliceResults
{
    public static IResult SlicePage(
        HttpRequest request,
        FeatureRegistry features,
        NavigationItem routeItem,
        RazorSlice detail,
        params RazorSlice[] additionalOobParts)
    {
        var pageRenderer = request.HttpContext.RequestServices.GetRequiredService<IFeaturePageRenderer>();
        var authService = request.HttpContext.RequestServices.GetService<IAuthorizationService>();
        var shellContext = features.CreateShellContext(routeItem, detail, request.HttpContext.User, authService);

        if (!request.IsHtmxRequest())
        {
            return pageRenderer.RenderPage(shellContext);
        }

        var oobParts = new List<RazorSlice> { pageRenderer.RenderNavigation(shellContext) };
        oobParts.AddRange(additionalOobParts);

        var rawOobParts = new List<IHtmlContent>();
        if (!string.IsNullOrWhiteSpace(shellContext.PageTitle))
        {
            rawOobParts.Add(CreatePageTitleOob(shellContext.PageTitle));
        }

        return HtmxFragment.Create(new HtmxFragmentModel(
            detail,
            oobParts,
            rawOobParts.Count > 0 ? rawOobParts : null));
    }

    private static IHtmlContent CreatePageTitleOob(string title)
    {
        var encoded = HtmlEncoder.Default.Encode(title);
        return new HtmlString($"<title hx-swap-oob=\"true\">{encoded}</title>");
    }
}