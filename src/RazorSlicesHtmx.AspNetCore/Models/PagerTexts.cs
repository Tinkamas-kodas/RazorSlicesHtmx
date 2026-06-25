namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Display text used by pager renderers and slices.
/// Provides English defaults; override with <c>new PagerTexts() with { Showing = "Rodomi", Of = "iš" }</c>
/// or construct with named arguments to supply translations.
/// </summary>
public record PagerTexts(
    string Showing  = "Showing",
    string Of       = "of",
    string NoItems  = "No items",
    string Ellipsis = "…",
    string PerPage  = "per page")
{
    public static readonly PagerTexts Default = new();
}
