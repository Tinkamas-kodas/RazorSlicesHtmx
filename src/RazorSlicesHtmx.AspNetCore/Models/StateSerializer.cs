using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Renders state HTML used by <see cref="IHasState"/> implementations.
/// Generated implementations delegate here to keep HTML-building logic out of generated code.
/// </summary>
public static class StateSerializer
{
    /// <summary>Renders a hidden-field div for inline (initial page) state persistence.</summary>
    public static IHtmlContent Serialize(string stateId, StateFieldValue[] fields)
    {
        var sb = new StringBuilder();
        sb.Append("<div id=\"").Append(HtmlEncoder.Default.Encode(stateId)).Append("\">");
        foreach (var field in fields)
        {
            sb.Append("<input type=\"hidden\" name=\"")
                .Append(HtmlEncoder.Default.Encode(field.Name))
                .Append("\" value=\"")
                .Append(HtmlEncoder.Default.Encode(field.Value))
                .Append("\">");
        }
        sb.Append("</div>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Renders an empty state container div with only the <c>id</c> attribute set, for use as an OOB swap target slot.</summary>
    public static IHtmlContent SerializeEmpty(string stateId)
    {
        return new HtmlString($"<div id=\"{HtmlEncoder.Default.Encode(stateId)}\"></div>");
    }

    /// <summary>Renders the same hidden-field div wrapped in an <c>hx-swap-oob</c> element for HTMX OOB updates.</summary>
    public static IHtmlContent SerializeOob(string stateId, StateFieldValue[] fields)
    {
        var encoded = HtmlEncoder.Default.Encode(stateId);
        var sb = new StringBuilder();
        sb.Append("<div hx-swap-oob=\"outerHTML:#").Append(encoded).Append("\">");
        sb.Append("<div id=\"").Append(encoded).Append("\">");
        foreach (var field in fields)
        {
            sb.Append("<input type=\"hidden\" name=\"")
                .Append(HtmlEncoder.Default.Encode(field.Name))
                .Append("\" value=\"")
                .Append(HtmlEncoder.Default.Encode(field.Value))
                .Append("\">");
        }
        sb.Append("</div></div>");
        return new HtmlString(sb.ToString());
    }
}
