using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.Bootstrap5.Options;

namespace RazorSlicesHtmx.Bootstrap5.Rendering;

/// <summary>
/// Bootstrap 5 implementation of <see cref="IListPagerRenderer"/>.
/// </summary>
public sealed class Bootstrap5PagerRenderer : IListPagerRenderer
{
    private readonly BootstrapPagerOptions _options;

    public Bootstrap5PagerRenderer(BootstrapPagerOptions? options = null)
    {
        _options = options ?? BootstrapPagerOptions.Default;
    }

    public IHtmlContent RenderPager(PagerModel model)
    {
        var t = model.EffectiveTexts;
        var sizes = model.EffectivePageSizeOptions;

        var sb = new StringBuilder();
        sb.Append("<div class=\"d-flex justify-content-between align-items-center\">");

        // Left: summary + page-size select
        sb.Append("<small class=\"text-muted\">");
        if (model.Total == 0)
        {
            sb.Append(HtmlEncoder.Default.Encode(t.NoItems));
        }
        else
        {
            sb.Append(HtmlEncoder.Default.Encode(t.Showing)).Append(' ')
              .Append(model.FromItem).Append('–').Append(model.ToItem).Append(' ')
              .Append(HtmlEncoder.Default.Encode(t.Of)).Append(' ')
              .Append(model.Total);
        }

        sb.Append("<span class=\"ms-2\">")
          .Append("<select class=\"form-select form-select-sm d-inline-block w-auto\"")
          .Append(" hx-get=\"").Append(HtmlEncoder.Default.Encode(model.GetUrl)).Append('"')
          .Append(" hx-push-url=\"true\"")
          .Append(" hx-trigger=\"change\"")
          .Append(" hx-vals='js:{\"PageSize\": event.target.value, \"Page\": 1}'");

        foreach (var s in sizes)
        {
            sb.Append("<option value=\"").Append(s).Append('"');
            if (s == model.PageSize) sb.Append(" selected");
            sb.Append('>').Append(s).Append(' ')
              .Append(HtmlEncoder.Default.Encode(t.PerPage))
              .Append("</option>");
        }

        sb.Append("</select></span></small>");

        // Right: page buttons
        if (model.TotalPages > 1)
        {
            sb.Append("<div class=\"btn-group btn-group-sm\" role=\"group\">");

            var pages = GetPagesToShow(model.CurrentPage, model.TotalPages);
            int? lastRendered = null;

            foreach (var p in pages)
            {
                if (lastRendered.HasValue && p - lastRendered.Value > 1)
                {
                    sb.Append("<button class=\"btn btn-outline-secondary\" type=\"button\" disabled>")
                      .Append(HtmlEncoder.Default.Encode(t.Ellipsis))
                      .Append("</button>");
                }

                var isActive = p == model.CurrentPage;
                sb.Append("<button class=\"btn ")
                  .Append(isActive ? _options.PagerActiveClass : _options.PagerInactiveClass)
                  .Append(" btn-sm\" type=\"button\"")
                  .Append(" hx-get=\"").Append(HtmlEncoder.Default.Encode(model.GetUrl)).Append('"')
                  .Append(" hx-push-url=\"true\"")
                  .Append(" hx-vals='{\"Page\":\"").Append(p).Append("\"}'");
                if (isActive) sb.Append(" aria-current=\"page\"");
                sb.Append('>').Append(p).Append("</button>");

                lastRendered = p;
            }

            sb.Append("</div>");
        }

        sb.Append("</div>");
        return new HtmlString(sb.ToString());
    }

    private static IReadOnlyList<int> GetPagesToShow(int page, int pages)
    {
        if (pages <= 5)
            return Enumerable.Range(1, pages).ToArray();

        var set = new SortedSet<int>
        {
            1, pages,
            page,
            Math.Max(1, page - 1),
            Math.Min(pages, page + 1)
        };
        return [.. set];
    }
}

