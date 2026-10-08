namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Standard paged lookup response payload consumed by UI renderers.
/// </summary>
public sealed record class LookupResponse(
    string Path,
    string Query,
    int Page,
    int NextPage,
    int PageSize,
    bool HasMore,
    IReadOnlyList<LookupItem> Items);
