namespace RazorSlicesHtmx.AspNetCore.Models;

public record ListResponse<T, TListRequest>(int Total, TListRequest Request, List<T> Items)
    where TListRequest : IListRequest
{
    public ListResponse() : this(0, default!, [])
    {
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, Request.PageSize)));

    public int CurrentPage => Math.Min(Request.Page, TotalPages);

    public int FromItem => Items.Count == 0 ? 0 : ((CurrentPage - 1) * Request.PageSize) + 1;

    public int ToItem => Items.Count == 0 ? 0 : FromItem + Items.Count - 1;
}