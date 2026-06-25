using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Results;

public static class SliceResults
{
    public static async Task<IResult> SlicePageAsync(
        HttpRequest request,
        FeatureRegistry features,
        NavigationRouteItem routeItem,
        Func<HttpContext, RazorSlice> detailFactory)
    {
        return await SlicePageAsync(request, features, routeItem, detailFactory(request.HttpContext));
    }

    public static async Task<IResult> SlicePageAsync(
        HttpRequest request,
        FeatureRegistry features,
        NavigationRouteItem routeItem,
        RazorSlice detail,
        params RazorSlice[] additionalOobParts)
    {
        var pageRenderer = request.HttpContext.RequestServices.GetRequiredService<IFeaturePageRenderer>();
        var transientUiRenderer = request.HttpContext.RequestServices.GetRequiredService<ITransientUiRenderer>();
        var options = request.HttpContext.RequestServices.GetRequiredService<IOptions<RazorSlicesHtmxOptions>>().Value;
        var authService = request.HttpContext.RequestServices.GetService<IAuthorizationService>();
        var shellContext = await features.CreateShellContextAsync(routeItem, detail, request.HttpContext.User, authService);

        if (!request.IsHtmxRequest())
        {
            return pageRenderer.RenderPage(shellContext);
        }

        var oobParts = new List<RazorSlice> { pageRenderer.RenderNavigation(shellContext) };
        oobParts.AddRange(CreateTransientOobParts(request, transientUiRenderer, options));
        oobParts.AddRange(additionalOobParts);

        return HtmxFragment.Create(new HtmxFragmentModel(detail, oobParts));
    }

    private static IReadOnlyList<RazorSlice> CreateTransientOobParts(
        HttpRequest request,
        ITransientUiRenderer transientUiRenderer,
        RazorSlicesHtmxOptions options)
    {
        if (!request.Headers.TryGetValue(TransientUiHeaders.FeatureToast, out var toastPayload))
        {
            return [];
        }

        try
        {
            var toasts = JsonSerializer.Deserialize<ToastModel[]>(toastPayload.ToString()) ?? [];
            return toasts
                .Select(toast => HtmxOob.Create(new HtmxOobModel(
                    options.ToastHostSelector,
                    options.DefaultOobSwap,
                    transientUiRenderer.RenderToast(toast))))
                .Cast<RazorSlice>()
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}