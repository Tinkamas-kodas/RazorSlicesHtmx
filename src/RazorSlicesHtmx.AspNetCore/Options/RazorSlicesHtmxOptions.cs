using RazorSlicesHtmx.AspNetCore.Htmx;

namespace RazorSlicesHtmx.AspNetCore.Options;

public sealed class RazorSlicesHtmxOptions
{
    public string DetailTargetSelector { get; set; } = "#details-pane";

    public string DialogHostSelector { get; set; } = "#dialog-host";

    public string? DialogClass { get; set; }
        = null;

    public string ToastHostSelector { get; set; } = "#toast-host";

    public int? ToastDelayMilliseconds { get; set; }
        = null;

    public string DefaultOobSwap { get; set; } = Hx.Swap.InnerHtml;

    /// <summary>
    /// Optional field options object. Bootstrap5 uses this to configure
    /// <c>BootstrapFieldOptions.Current</c> at startup.
    /// </summary>
    public object? FieldOptions { get; set; }
}