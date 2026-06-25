namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>Data passed to pager renderers and the <c>_Pager</c> slice.</summary>
public record PagerModel(
    string GetUrl,
    int TotalPages,
    int CurrentPage,
    int PageSize,
    int Total,
    int FromItem,
    int ToItem,
    IReadOnlyList<int>? PageSizeOptions = null,
    PagerTexts? Texts = null)
{
    /// <summary>Default page-size choices used when <see cref="PageSizeOptions"/> is <c>null</c>.</summary>
    public static readonly IReadOnlyList<int> DefaultPageSizeOptions = [5, 10, 20];

    public IReadOnlyList<int> EffectivePageSizeOptions => PageSizeOptions ?? DefaultPageSizeOptions;
    public PagerTexts EffectiveTexts => Texts ?? PagerTexts.Default;
}
