namespace RazorSlicesHtmx.AspNetCore.Models;

public static class PageableListExtensions
{
    public static ListResponse<T, TListRequest> ToPagedList<T, TListRequest>(
        this IQueryable<T> source,
        TListRequest request) where TListRequest : IListRequest<T>
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy) && request.SortMap.TryGetValue(request.SortBy, out var sortExpr))
        {
            source = request.SortDirection == SortDirection.Desc
                ? source.OrderByDescending(sortExpr)
                : source.OrderBy(sortExpr);
        }

        return ToPagedList<T, TListRequest, ListResponse<T, TListRequest>>(source, request);
    }

    public static TListResponse ToPagedList<T, TListRequest, TListResponse>(
        this IQueryable<T> source,
        TListRequest request)
        where TListRequest : IListRequest
        where TListResponse : ListResponse<T, TListRequest>, new()
    {
        var (requestedPage, normalizedPageSize) = request.NormalizePaging(defaultPage: 1, defaultPageSize: 10);

        var totalItems = source.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)normalizedPageSize));
        var currentPage = Math.Min(requestedPage, totalPages);

        request.SetPaging(currentPage, normalizedPageSize);

        var pagedQuery = source
            .Skip((currentPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize);

        var items = pagedQuery.ToList();

        return new TListResponse
        {
            Items = items,
            Request = request,
            Total = totalItems
        };
    }

    public static Task<ListResponse<T, TListRequest>> ToPagedListAsync<T, TListRequest>(
        this IQueryable<T> source,
        TListRequest request,
        CancellationToken ct = default) where TListRequest : IListRequest<T>
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy) && request.SortMap.TryGetValue(request.SortBy, out var sortExpr))
        {
            source = request.SortDirection == SortDirection.Desc
                ? source.OrderByDescending(sortExpr)
                : source.OrderBy(sortExpr);
        }

        return ToPagedListAsync<T, TListRequest, ListResponse<T, TListRequest>>(source, request, ct);
    }

    public static Task<TListResponse> ToPagedListAsync<T, TListRequest, TListResponse>(
        this IQueryable<T> source,
        TListRequest request,
        CancellationToken ct = default)
        where TListRequest : IListRequest
        where TListResponse : ListResponse<T, TListRequest>, new()
        => Task.FromResult(ToPagedList<T, TListRequest, TListResponse>(source, request));
}