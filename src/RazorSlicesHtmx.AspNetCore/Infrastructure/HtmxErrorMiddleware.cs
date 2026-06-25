using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.AspNetCore.Results;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Infrastructure;

/// <summary>
/// Middleware that intercepts unhandled exceptions and non-success status codes
/// for HTMX requests, returning an error toast instead of a raw error page.
/// For non-HTMX requests the exception is re-thrown to be handled by the standard pipeline.
/// </summary>
public sealed class HtmxErrorMiddleware(
    RequestDelegate next,
    IOptions<HtmxErrorOptions> errorOptions,
    ILogger<HtmxErrorMiddleware> logger)
{
    private readonly HtmxErrorOptions _errorOptions = errorOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.IsHtmxRequest())
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);

            if (context.Response.StatusCode >= 400 && !context.Response.HasStarted)
            {
                await WriteErrorToastAsync(context, context.Response.StatusCode);
            }
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception during HTMX request {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await WriteErrorToastAsync(context, 500, ex);
        }
    }

    private async Task WriteErrorToastAsync(HttpContext context, int statusCode, Exception? exception = null)
    {
        var message = _errorOptions.FormatMessage?.Invoke(statusCode, exception)
            ?? GetDefaultMessage(statusCode);

        var title = _errorOptions.FormatTitle?.Invoke(statusCode, exception)
            ?? GetDefaultTitle(statusCode);

        var tone = _errorOptions.ErrorTone ?? "error";

        var renderer = context.RequestServices.GetService<ITransientUiRenderer>();
        var options = context.RequestServices.GetService<IOptions<RazorSlicesHtmxOptions>>()?.Value;

        if (renderer is null || options is null)
        {
            // No renderer registered — write a minimal fallback response
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            context.Response.Headers.Append("HX-Reswap", "none");
            await context.Response.WriteAsync(message);
            return;
        }

        var toast = new ToastModel(title, message, tone);
        var toastSlice = renderer.RenderToast(toast);
        var hostSelector = options.ToastHostSelector;
        var swap = options.DefaultOobSwap;

        var fragment = HtmxFragmentResult.Create(HtmxOob.Create(new HtmxOobModel(hostSelector, swap, toastSlice)))
            .Build();

        context.Response.StatusCode = 200; // HTMX ignores non-2xx by default
        await fragment.ExecuteAsync(context);
    }

    private static string GetDefaultTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Validation Error",
        429 => "Too Many Requests",
        >= 500 => "Server Error",
        _ => "Error"
    };

    private static string GetDefaultMessage(int statusCode) => statusCode switch
    {
        400 => "The request could not be processed.",
        401 => "You must be logged in to perform this action.",
        403 => "You do not have permission to perform this action.",
        404 => "The requested resource was not found.",
        409 => "A conflict occurred while processing the request.",
        422 => "The submitted data is invalid.",
        429 => "Too many requests. Please try again later.",
        >= 500 => "An unexpected error occurred. Please try again.",
        _ => "An error occurred while processing the request."
    };
}
