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

    public string DefaultOobSwap { get; set; } = HtmxSwap.InnerHtml;
}