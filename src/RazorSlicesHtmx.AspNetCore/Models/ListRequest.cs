using System.Linq.Expressions;

namespace RazorSlicesHtmx.AspNetCore.Models;

public interface IListRequest<T> : IListRequest
{
    IReadOnlyDictionary<string, Expression<Func<T, object?>>> SortMap { get; }
}

public abstract class ListRequest<T> : IListRequest<T>
{
    [StateNotMap]
    public virtual IReadOnlyDictionary<string, Expression<Func<T, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<T, object?>>>(StringComparer.OrdinalIgnoreCase);

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? SortBy { get; set; }

    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    protected readonly record struct CommonQueryValues(string? SortBy, SortDirection SortDirection, int Page, int PageSize);

    protected static CommonQueryValues ReadCommonQuery(
        IQueryCollection query,
        int defaultPage,
        int defaultPageSize,
        int maxPageSize)
    {
        var parsedPage = int.TryParse(query["Page"], out var page) ? page : defaultPage;
        var parsedPageSize = int.TryParse(query["PageSize"], out var pageSize) ? pageSize : defaultPageSize;
        var normalizedPage = parsedPage < 1 ? defaultPage : parsedPage;
        var normalizedPageSize = parsedPageSize < 1 || parsedPageSize > maxPageSize
            ? defaultPageSize
            : parsedPageSize;

        return new CommonQueryValues(
            query["SortBy"].ToString(),
            ParseSortDirection(query["SortDirection"].ToString()),
            normalizedPage,
            normalizedPageSize);
    }

    protected static string NormalizeSortBy(string? sortBy, IEnumerable<string> allowedSortColumns, string defaultSortBy)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSortBy))
        {
            return defaultSortBy;
        }

        foreach (var column in allowedSortColumns)
        {
            if (string.Equals(column, normalizedSortBy, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedSortBy;
            }
        }

        return defaultSortBy;
    }

    protected static SortDirection ParseSortDirection(string? raw) =>
        Enum.TryParse<SortDirection>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : SortDirection.Asc;

}