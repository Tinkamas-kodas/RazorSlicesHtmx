using System.Diagnostics;
using System.Security.Claims;

namespace Apskaita.Shared.Logging;

/// <summary>
/// Logs every request and its outcome: method, path, query, HTMX headers, response status, elapsed time,
/// and the authenticated user (or anonymous). Registered first in the pipeline so it wraps static files,
/// authentication, authorization, and endpoints.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Apskaita.Requests");

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var started = Stopwatch.GetTimestamp();
        var htmx = string.Equals(request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
        var htmxTarget = request.Headers["HX-Target"].ToString();

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UNHANDLED {Method} {Path} htmx={Htmx} htmxTarget={HtmxTarget}", request.Method, request.Path, htmx, htmxTarget);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var user = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(ClaimTypes.Name)?.Value ?? "(authenticated)"
                : "(anonymous)";
            var location = context.Response.Headers["Location"].ToString();
            var hxRedirect = context.Response.Headers["HX-Redirect"].ToString();

            _logger.LogInformation("{Status} {Method} {Path} {Elapsed} user={User} location={Location} htmx={Htmx} htmxTarget={HtmxTarget} hxRedirect={HxRedirect} ",
                context.Response.StatusCode, request.Method, request.Path,
                $"{elapsed.TotalMilliseconds:F0}ms", user,
                location.Length == 0 ? "-" : location,
                htmx,
                htmxTarget,
                hxRedirect.Length == 0 ? "-" : hxRedirect);
        }
    }
}

/// <summary>Pipeline registration for <see cref="RequestLoggingMiddleware"/>.</summary>
public static class RequestLoggingExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
