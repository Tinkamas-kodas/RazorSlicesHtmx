using Microsoft.AspNetCore.Html;
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

