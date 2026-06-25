namespace RazorSlicesHtmx.AspNetCore.Infrastructure;

/// <summary>
/// Configuration options for <see cref="HtmxErrorMiddleware"/>.
/// </summary>
public sealed class HtmxErrorOptions
{
    /// <summary>
    /// Custom title formatter. Receives the HTTP status code and optional exception.
    /// When null, a built-in default title is used (e.g. "Server Error", "Not Found").
    /// </summary>
    public Func<int, Exception?, string>? FormatTitle { get; set; }

    /// <summary>
    /// Custom message formatter. Receives the HTTP status code and optional exception.
    /// When null, a built-in default message is used.
    /// </summary>
    public Func<int, Exception?, string>? FormatMessage { get; set; }
}
