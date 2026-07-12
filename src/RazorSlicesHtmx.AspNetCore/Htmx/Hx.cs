namespace RazorSlicesHtmx.AspNetCore.Htmx;

/// <summary>HTMX HTTP header name constants.</summary>
public static class Hx
{
    // ── Request headers ──────────────────────────────────────────────────────
    public const string Request = "HX-Request";
    public const string Target = "HX-Target";
    public const string CurrentUrl = "HX-Current-URL";

    // ── Response headers ─────────────────────────────────────────────────────
    public const string Location = "HX-Location";
    public const string PushUrl = "HX-Push-Url";
    public const string ReplaceUrl = "HX-Replace-Url";
    public const string Reswap = "HX-Reswap";
    public const string Retarget = "HX-Retarget";
    public const string Trigger = "HX-Trigger";
    public const string TriggerAfterSwap = "HX-Trigger-After-Swap";

    // ── Swap strategies ──────────────────────────────────────────────────────
    /// <summary>
    /// Canonical HTMX/DOM swap values with the exact casing expected by client-side swap semantics.
    /// </summary>
    public static class Swap
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
}
