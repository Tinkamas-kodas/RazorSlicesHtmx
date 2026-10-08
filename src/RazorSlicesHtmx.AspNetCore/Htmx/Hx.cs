namespace RazorSlicesHtmx.AspNetCore.Htmx;

/// <summary>HTMX HTTP header name constants.</summary>
public static class Hx
{
    // ── Request headers ──────────────────────────────────────────────────────
    public const string Request = "HX-Request";
    public const string HistoryRestoreRequest = "HX-History-Restore-Request";
    /// <summary>
    /// New in htmx 4: <c>"partial"</c> for targeted swaps, <c>"full"</c> for body-level requests.
    /// </summary>
    public const string RequestType = "HX-Request-Type";
    public const string Target = "HX-Target";
    public const string CurrentUrl = "HX-Current-URL";

    // ── Response headers ─────────────────────────────────────────────────────
    public const string Location = "HX-Location";
    public const string PushUrl = "HX-Push-Url";
    public const string ReplaceUrl = "HX-Replace-Url";
    public const string Reswap = "HX-Reswap";
    public const string Retarget = "HX-Retarget";
    /// <summary>
    /// In htmx 4 this header fires after the swap completes.
    /// </summary>
    public const string Trigger = "HX-Trigger";

    // ── Swap strategies ──────────────────────────────────────────────────────
    /// <summary>
    /// Canonical HTMX/DOM swap values with the exact casing expected by client-side swap semantics.
    /// </summary>
    public static class Swap
    {
        public const string InnerHtml = "innerHTML";
        public const string OuterHtml = "outerHTML";
        /// <summary>htmx 4: morphs the children of the target element, preserving existing DOM nodes.</summary>
        public const string InnerMorph = "innerMorph";
        public const string BeforeBegin = "beforebegin";
        public const string AfterBegin = "afterbegin";
        public const string BeforeEnd = "beforeend";
        public const string AfterEnd = "afterend";
        public const string Delete = "delete";
        public const string None = "none";
    }
}
