using RazorSlicesHtmx.AspNetCore.Htmx;

namespace RazorSlicesHtmx.AspNetCore.Infrastructure;

internal static class HtmxRequestExtensions
{
    public static bool IsHtmxRequest(this HttpRequest request) =>
        string.Equals(request.Headers[Hx.Request], "true", StringComparison.OrdinalIgnoreCase);
}