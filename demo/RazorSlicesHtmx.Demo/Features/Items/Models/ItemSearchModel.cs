namespace RazorSlicesHtmx.Demo.Features.Items.Models;

/// <summary>Search/filter state for the Items list. Paging and sort state lives separately in <see cref="ItemListQuery"/>.</summary>
public sealed partial record ItemSearchModel(string? Search);
