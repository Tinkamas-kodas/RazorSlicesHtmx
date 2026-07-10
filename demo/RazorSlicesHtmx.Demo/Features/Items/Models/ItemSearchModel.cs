namespace RazorSlicesHtmx.Demo.Features.Items.Models;

/// <summary>Search/filter state for the Items list. Paging and sort state lives separately in <see cref="ItemListQuery"/>.</summary>
[RazorSlicesHtmx.HtmlNames.GenerateHtmlNames]
public sealed partial record ItemSearchModel(string? Search): IHaveValidationResult
{
    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; }
}


public sealed class LayoutModel(ItemSearchModel searchState, ItemListQuery listState)
{
    public ItemSearchModel SearchState { get; } = searchState;
    public ItemListQuery ListState { get; } = listState;
}