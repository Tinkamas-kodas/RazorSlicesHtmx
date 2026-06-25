using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Renders a sort header button for a single column.
/// </summary>
public interface IListSortHeaderRenderer
{
    IHtmlContent RenderHeader(SortButtonModel model);
}
