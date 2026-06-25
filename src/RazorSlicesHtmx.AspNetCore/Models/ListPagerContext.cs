using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Renders a pager for a paged list.
/// Obtain via <c>response.PagerContext(url, renderer)</c> or a framework-specific
/// extension such as <c>response.PagerContext(url, BootstrapPagerOptions.Default with { ... })</c>.
/// </summary>
public sealed class ListPagerContext
{
    private readonly PagerModel _model;
    private readonly IListPagerRenderer _renderer;

    internal ListPagerContext(PagerModel model, IListPagerRenderer renderer)
    {
        _model = model;
        _renderer = renderer;
    }

    /// <summary>
    /// Renders the pager. Returns <see cref="HtmlString.Empty"/> when there is only one page.
    /// </summary>
    public IHtmlContent Render() => _renderer.RenderPager(_model);
}
