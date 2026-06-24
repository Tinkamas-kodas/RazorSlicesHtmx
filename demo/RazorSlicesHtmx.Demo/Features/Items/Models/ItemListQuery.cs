namespace RazorSlicesHtmx.Demo.Features.Items.Models;

public sealed record ItemListQuery(
    string? Search = null,
    string SortBy = "code",
    string SortDir = "asc",
    int Page = 1,
    int PageSize = 5)
{
    public bool HasSearch => !string.IsNullOrWhiteSpace(Search);

    public ItemListQuery Normalize()
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        var normalizedSortBy = SortBy?.Trim().ToLowerInvariant() switch
        {
            "name" => "name",
            "isenabled" => "isenabled",
            _ => "code"
        };

        var normalizedSortDir = string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";

        var normalizedPage = Page < 1 ? 1 : Page;
        var normalizedPageSize = PageSize is < 1 or > 50 ? 5 : PageSize;

        return this with
        {
            Search = normalizedSearch,
            SortBy = normalizedSortBy,
            SortDir = normalizedSortDir,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };
    }

    public ItemListQuery ForSort(string sortBy)
    {
        var normalizedSortBy = sortBy.Trim().ToLowerInvariant();
        var nextSortDir = normalizedSortBy == SortBy && SortDir == "asc" ? "desc" : "asc";

        return this with
        {
            SortBy = normalizedSortBy,
            SortDir = nextSortDir,
            Page = 1
        };
    }

    public ItemListQuery ForPage(int page) => this with { Page = page < 1 ? 1 : page };

    public ItemListQuery ClearSearch() => this with { Search = null, Page = 1 };

    public string ToQueryString()
    {
        var values = new List<string>();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            values.Add($"Search={Uri.EscapeDataString(Search)}");
        }

        values.Add($"SortBy={Uri.EscapeDataString(SortBy)}");
        values.Add($"SortDir={Uri.EscapeDataString(SortDir)}");
        values.Add($"Page={Page}");
        values.Add($"PageSize={PageSize}");

        return string.Join("&", values);
    }

    public string ToRoute(string path)
    {
        var queryString = ToQueryString();
        return string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
    }
}