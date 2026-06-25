using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Renders sort header buttons for a list backed by <see cref="IListRequest"/>.
/// Obtain via <c>response.SortHeader(url, renderer)</c> or a framework-specific
/// extension such as <c>response.SortHeader(url, BootstrapHeaderOptions.Default with { ... })</c>.
/// </summary>
public sealed class ListSortHeaderContext
{
    private readonly IListRequest _request;
    private readonly string _getUrl;
    private readonly IListSortHeaderRenderer _renderer;

    internal ListSortHeaderContext(IListRequest request, string getUrl, IListSortHeaderRenderer renderer)
    {
        _request = request;
        _getUrl = getUrl;
        _renderer = renderer;
    }

    /// <summary>
    /// Renders a sort header button for <paramref name="column"/>.
    /// The button carries <c>hx-vals</c> with <c>SortBy</c> and the toggled <c>SortDirection</c>.
    /// <c>hx-include</c> is intentionally omitted — it is inherited from the enclosing element.
    /// </summary>
    public IHtmlContent Render(string column, string label)
        => _renderer.RenderHeader(GetModel(column, label));

    /// <summary>
    /// Returns the <see cref="SortButtonModel"/> for <paramref name="column"/> without rendering it.
    /// Use this when rendering via a Razor slice:
    /// <c>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("code", "Code")))</c>
    /// </summary>
    public SortButtonModel GetModel(string column, string label)
    {
        var nextDir = _request.NextSortDirection(column);
        var isActive = string.Equals(_request.SortBy, column, StringComparison.OrdinalIgnoreCase);
        var state = !isActive ? SortState.Inactive :
            _request.SortDirection == SortDirection.Asc ? SortState.SortedAsc : SortState.SortedDesc;
        return new SortButtonModel(column, label, _getUrl, nextDir, state);
    }
}
