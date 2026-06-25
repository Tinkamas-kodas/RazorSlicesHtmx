namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>Data passed to <see cref="IListSortRenderer.RenderHeader"/> for each sort column button.</summary>
public record SortButtonModel(
    string Column,
    string Label,
    string GetUrl,
    SortDirection NextDirection,
    SortState State);
