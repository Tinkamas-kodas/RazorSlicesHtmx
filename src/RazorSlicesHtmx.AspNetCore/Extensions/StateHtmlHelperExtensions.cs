using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using RazorSlicesHtmx.AspNetCore.Htmx;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Extensions;

public static class StateHtmlHelperExtensions
{
    /// <summary>
    /// Renders the hidden-field state div inline (alias for <see cref="IHasState.Serialize"/>).
    /// Use in the host slice where state must be present on initial full-page render.
    /// </summary>
    public static IHtmlContent IncludeState(this IHasState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Serialize();
    }
}

public static class HtmlHelperExtensions
{
    public static bool IsHtmx(this IHtmlHelper helper)
    {
        ArgumentNullException.ThrowIfNull(helper);
        return helper.ViewContext.HttpContext.IsHtmx();
    }

    public static bool IsHtmx(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return string.Equals(httpContext.Request.Headers[Hx.Request], "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHtmxTarget(this HttpContext httpContext, string target)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return string.Equals(httpContext.Request.Headers[Hx.Target], target,
            StringComparison.OrdinalIgnoreCase);
    }
}

