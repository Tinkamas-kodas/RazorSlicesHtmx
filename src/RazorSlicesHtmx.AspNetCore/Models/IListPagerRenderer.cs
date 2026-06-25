using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Renders a pager for a paged list.
/// </summary>
public interface IListPagerRenderer
{
    IHtmlContent RenderPager(PagerModel model);
}
