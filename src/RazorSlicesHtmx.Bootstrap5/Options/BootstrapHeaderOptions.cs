namespace RazorSlicesHtmx.Bootstrap5.Options;

/// <summary>
/// CSS/glyph options for Bootstrap 5 sort header buttons.
/// Use <c>BootstrapHeaderOptions.Default with { ... }</c> to override specific values.
/// </summary>
public record BootstrapHeaderOptions(
    string SortButtonClass,
    string GlyphSpanClass,
    string GlyphAsc,
    string GlyphDesc,
    string GlyphNeutral)
{
    public static readonly BootstrapHeaderOptions Default = new(
        SortButtonClass: "btn btn-link p-0 text-decoration-none",
        GlyphSpanClass: string.Empty,
        GlyphAsc: "↑",
        GlyphDesc: "↓",
        GlyphNeutral: "↕");
}
