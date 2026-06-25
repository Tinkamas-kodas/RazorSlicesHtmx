using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Implemented by types whose instances are persisted as hidden-field state across HTMX interactions.
/// The implementation is generated automatically for any <c>partial</c> type passed to <c>WithState(...)</c>.
/// </summary>
public interface IHasState
{
    /// <summary>Renders a <c>&lt;div id="..."&gt;</c> with hidden inputs for each state property (inline use in Razor).</summary>
    IHtmlContent Serialize();

    /// <summary>Renders the same div wrapped in <c>hx-swap-oob</c> for out-of-band HTMX updates.</summary>
    IHtmlContent SerializeOob();
}
