using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.Bootstrap5.Options;
using RazorSlicesHtmx.Bootstrap5.Rendering;

namespace RazorSlicesHtmx.Bootstrap5.Extensions;

public static class BootstrapListSortExtensions
{
    private static readonly Bootstrap5SortHeaderRenderer DefaultHeaderRenderer = new();
    private static readonly Bootstrap5PagerRenderer DefaultPagerRenderer = new();

    /// <summary>
    /// Creates a <see cref="ListSortHeaderContext"/> using Bootstrap 5 defaults.
    /// Pass <c>BootstrapHeaderOptions.Default with { ... }</c> to override specific CSS classes.
    /// </summary>
    public static ListSortHeaderContext SortHeader<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl,
        BootstrapHeaderOptions? options = null)
        where TListRequest : IListRequest
        => response.SortHeader(getUrl,
            options is null ? DefaultHeaderRenderer : new Bootstrap5SortHeaderRenderer(options));

    /// <summary>
    /// Creates a <see cref="ListPagerContext"/> using Bootstrap 5 defaults.
    /// Pass <c>BootstrapPagerOptions.Default with { ... }</c> to override specific CSS classes.
    /// Pass <c>texts</c> to supply translated display strings.
    /// </summary>
    public static ListPagerContext PagerContext<T, TListRequest>(
        this ListResponse<T, TListRequest> response,
        string getUrl,
        BootstrapPagerOptions? options = null,
        PagerTexts? texts = null)
        where TListRequest : IListRequest
    {
        var renderer = options is null ? DefaultPagerRenderer : new Bootstrap5PagerRenderer(options);
        return texts is null
            ? response.PagerContext(getUrl, renderer)
            : response.PagerContext(getUrl, renderer, texts);
    }
}
