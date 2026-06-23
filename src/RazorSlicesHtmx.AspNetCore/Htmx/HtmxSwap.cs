namespace RazorSlicesHtmx.AspNetCore.Htmx;

/// <summary>
/// Canonical HTMX/DOM swap values with the exact casing expected by client-side swap semantics.
/// </summary>
public static class HtmxSwap
{
    public const string InnerHtml = "innerHTML";
    public const string OuterHtml = "outerHTML";
    public const string BeforeBegin = "beforebegin";
    public const string AfterBegin = "afterbegin";
    public const string BeforeEnd = "beforeend";
    public const string AfterEnd = "afterend";
    public const string Delete = "delete";
    public const string None = "none";
}