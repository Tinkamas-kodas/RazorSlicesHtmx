using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.Bootstrap5.Options;

namespace RazorSlicesHtmx.Bootstrap5.Rendering;

/// <summary>
/// Bootstrap 5 implementation of <see cref="IListSortHeaderRenderer"/>.
/// </summary>
public sealed class Bootstrap5SortHeaderRenderer : IListSortHeaderRenderer
{
    private readonly BootstrapHeaderOptions _options;

    public Bootstrap5SortHeaderRenderer(BootstrapHeaderOptions? options = null)
    {
        _options = options ?? BootstrapHeaderOptions.Default;
    }

    public IHtmlContent RenderHeader(SortButtonModel model)
    {
        var glyph = model.State switch
        {
            SortState.SortedAsc  => _options.GlyphAsc,
            SortState.SortedDesc => _options.GlyphDesc,
            _                    => _options.GlyphNeutral
        };

        var sb = new StringBuilder();
        sb.Append("<button");
        AppendClassAttr(sb, _options.SortButtonClass);
        sb.Append(" type=\"button\"");
        sb.Append(" hx-get=\"").Append(HtmlEncoder.Default.Encode(model.GetUrl)).Append('"');
        sb.Append(" hx-push-url=\"true\"");
        sb.Append(" hx-vals='{\"SortBy\":\"").Append(model.Column)
          .Append("\",\"SortDirection\":\"").Append(model.NextDirection).Append("\"}'");
        sb.Append('>');
        sb.Append(HtmlEncoder.Default.Encode(model.Label));
        if (!string.IsNullOrEmpty(glyph))
        {
            sb.Append(" <span");
            AppendClassAttr(sb, _options.GlyphSpanClass);
            sb.Append('>');
            sb.Append(HtmlEncoder.Default.Encode(glyph));
            sb.Append("</span>");
        }
        sb.Append("</button>");
        return new HtmlString(sb.ToString());
    }

    private static void AppendClassAttr(StringBuilder sb, string? cssClass)
    {
        if (!string.IsNullOrEmpty(cssClass))
            sb.Append(" class=\"").Append(HtmlEncoder.Default.Encode(cssClass)).Append('"');
    }
}
