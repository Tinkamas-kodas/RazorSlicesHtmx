namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Common lookup query parameters for searchable paged endpoints.
/// </summary>
public sealed class LookupRequest
{
    public string? Path { get; set; }

    public string? Query { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public void Normalize(int defaultPageSize = 10, int maxPageSize = 100)
    {
        Page = Page < 1 ? 1 : Page;
        PageSize = PageSize < 1 ? defaultPageSize : Math.Min(PageSize, maxPageSize);
        Query = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
        Path = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim();
    }

    public LookupResponse ToResponse(IReadOnlyList<LookupItem> items, bool hasMore)
    {
        Normalize();
        return new LookupResponse(
            Path: Path ?? string.Empty,
            Query: Query ?? string.Empty,
            Page: Page,
            NextPage: Page + 1,
            PageSize: PageSize,
            HasMore: hasMore,
            Items: items);
    }
}
