using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.Demo.Features.Lookup.Models;

namespace RazorSlicesHtmx.Demo.Features.Lookup.Services;

public sealed class LookupContentService
{
    private static readonly IReadOnlyList<(string Id, string Name)> Catalog = BuildCatalog();

    public LookupPageModel CreatePageModel()
    {
        var request = new LookupDemoRequest();
        return new LookupPageModel(request, BuildSummary(request));
    }

    public LookupSummaryModel BuildSummary(LookupDemoRequest request)
    {
        var requester = string.IsNullOrWhiteSpace(request.Requester)
            ? "Anonymous requester"
            : request.Requester.Trim();

        var selected = Catalog.FirstOrDefault(x =>
            string.Equals(x.Id, request.ClientId, StringComparison.OrdinalIgnoreCase));

        var selectedName = string.IsNullOrWhiteSpace(selected.Id) ? null : selected.Name;
        return new LookupSummaryModel(requester, request.ClientId, selectedName);
    }

    public LookupResponse Search(LookupRequest request)
    {
        request.Normalize(defaultPageSize: 12, maxPageSize: 40);

        var query = request.Query;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? Catalog
            : Catalog.Where(x =>
                x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Id.Contains(query, StringComparison.OrdinalIgnoreCase));

        var skip = (request.Page - 1) * request.PageSize;
        var pageSlice = filtered
            .Skip(skip)
            .Take(request.PageSize + 1)
            .ToList();

        var hasMore = pageSlice.Count > request.PageSize;
        var items = pageSlice
            .Take(request.PageSize)
            .Select(x => new LookupItem(x.Id, x.Name))
            .ToList();

        return request.ToResponse(items, hasMore);
    }

    public LookupItem? ResolveSelected(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var selected = Catalog.FirstOrDefault(x =>
            string.Equals(x.Id, value, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(selected.Id)
            ? null
            : new LookupItem(selected.Id, selected.Name);
    }

    private static IReadOnlyList<(string Id, string Name)> BuildCatalog()
    {
        var list = new List<(string Id, string Name)>(220);
        for (var i = 1; i <= 220; i++)
        {
            list.Add(($"CLI-{i:0000}", $"Client {i:0000} - Sector {(i % 9) + 1}"));
        }

        return list;
    }
}
