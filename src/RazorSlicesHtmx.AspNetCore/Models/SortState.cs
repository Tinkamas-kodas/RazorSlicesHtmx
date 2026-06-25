namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>Sort state of a column used by <see cref="SortButtonModel"/> to communicate current sort to a renderer.</summary>
public enum SortState
{
    Inactive,
    SortedAsc,
    SortedDesc
}
