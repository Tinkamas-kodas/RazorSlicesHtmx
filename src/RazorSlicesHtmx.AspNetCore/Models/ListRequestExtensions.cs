namespace RazorSlicesHtmx.AspNetCore.Models;

public static class ListRequestExtensions
{
    public static SortDirection NextSortDirection(this IListRequest request, string column) =>
        request.SortBy == null ? SortDirection.Asc :
            !string.Equals(request.SortBy, column, StringComparison.OrdinalIgnoreCase) ? SortDirection.Asc :
                request.SortDirection == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;

    public static (int RequestedPage, int NormalizedPageSize) NormalizePaging(
        this IListRequest request,
        int defaultPage = 1,
        int defaultPageSize = 10,
        int? maxPageSize = null)
    {
        var requestedPage = request.Page < 1 ? defaultPage : request.Page;

        var normalizedPageSize = request.PageSize < 1
            ? defaultPageSize
            : request.PageSize;

        if (maxPageSize is { } max && normalizedPageSize > max)
        {
            normalizedPageSize = max;
        }

        return (requestedPage, normalizedPageSize);
    }

    public static void SetPaging(this IListRequest request, int page, int pageSize)
    {
        request.Page = page;
        request.PageSize = pageSize;
    }

    /// <summary>
    /// Creates a <see cref="ListSortHeaderContext"/> for rendering sort header buttons.
    /// </summary>
    public static ListSortHeaderContext SortHeader<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl,
        IListSortHeaderRenderer renderer)
        where TListRequest : IListRequest
        => new(response.Request, getUrl, renderer);

    /// <summary>
    /// Creates a <see cref="ListPagerContext"/> for rendering a pager.
    /// </summary>
    public static ListPagerContext PagerContext<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl,
        IListPagerRenderer renderer)
        where TListRequest : IListRequest
        => new(new PagerModel(getUrl, response.TotalPages, response.CurrentPage,
                   response.Request.PageSize, response.Total,
                   response.FromItem, response.ToItem),
               renderer);

    /// <summary>
    /// Creates a <see cref="ListPagerContext"/> with translated display text.
    /// </summary>
    public static ListPagerContext PagerContext<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl,
        IListPagerRenderer renderer,
        PagerTexts texts)
        where TListRequest : IListRequest
        => new(new PagerModel(getUrl, response.TotalPages, response.CurrentPage,
                   response.Request.PageSize, response.Total,
                   response.FromItem, response.ToItem, Texts: texts),
               renderer);

    /// <summary>
    /// Extracts a <see cref="PagerModel"/> from the response.
    /// Use this when rendering the pager via a Razor slice:
    /// <c>@await RenderPartialAsync(_Pager.For, Model.ToPagerModel("/items/list"))</c>
    /// </summary>
    public static PagerModel ToPagerModel<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl)
        where TListRequest : IListRequest
        => new(getUrl, response.TotalPages, response.CurrentPage,
               response.Request.PageSize, response.Total,
               response.FromItem, response.ToItem);

    /// <summary>
    /// Extracts a <see cref="PagerModel"/> with translated display text.
    /// </summary>
    public static PagerModel ToPagerModel<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl,
        PagerTexts texts)
        where TListRequest : IListRequest
        => new(getUrl, response.TotalPages, response.CurrentPage,
               response.Request.PageSize, response.Total,
               response.FromItem, response.ToItem, Texts: texts);
}
