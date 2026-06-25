namespace RazorSlicesHtmx.Bootstrap5.Options;

/// <summary>
/// CSS options for the Bootstrap 5 pager.
/// Use <c>BootstrapPagerOptions.Default with { ... }</c> to override specific values.
/// </summary>
public record BootstrapPagerOptions(
    string PagerContainerClass,
    string PagerActiveClass,
    string PagerInactiveClass)
{
    public static readonly BootstrapPagerOptions Default = new(
        PagerContainerClass: string.Empty,
        PagerActiveClass: "btn btn-dark btn-sm",
        PagerInactiveClass: "btn btn-outline-dark btn-sm");
}
