using Microsoft.Extensions.DependencyInjection;
using RazorSlicesHtmx.AspNetCore.Infrastructure;

namespace RazorSlicesHtmx.AspNetCore.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds HTMX-aware error handling middleware. When an HTMX request results in an
    /// unhandled exception or a non-success status code, the middleware returns an
    /// error toast instead of a raw error page. Non-HTMX requests are unaffected.
    /// </summary>
    public static IApplicationBuilder UseHtmxErrorHandling(
        this IApplicationBuilder app,
        Action<HtmxErrorOptions>? configure = null)
    {
        if (configure is not null)
        {
            var options = app.ApplicationServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<HtmxErrorOptions>>()
                .CurrentValue;
            configure(options);
        }

        app.UseMiddleware<HtmxErrorMiddleware>();
        return app;
    }
}
