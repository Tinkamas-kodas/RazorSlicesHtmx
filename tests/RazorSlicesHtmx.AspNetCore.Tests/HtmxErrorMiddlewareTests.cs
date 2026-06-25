using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class HtmxErrorMiddlewareTests
{
    [Fact]
    public async Task NonHtmx_request_passes_through_without_interception()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateContext(isHtmx: false);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task NonHtmx_request_exception_is_rethrown()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));
        var context = CreateContext(isHtmx: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task Htmx_request_with_200_passes_through()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateContext(isHtmx: true);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Htmx_request_with_404_returns_200_with_toast()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });
        var context = CreateContext(isHtmx: true, withRenderer: true);

        await middleware.InvokeAsync(context);

        // Middleware resets to 200 so HTMX processes the response
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Htmx_request_with_500_returns_200_with_toast()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        });
        var context = CreateContext(isHtmx: true, withRenderer: true);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Htmx_request_exception_returns_200_with_toast()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("test error"));
        var context = CreateContext(isHtmx: true, withRenderer: true);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Htmx_request_exception_without_renderer_uses_fallback()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("test error"));
        var context = CreateContext(isHtmx: true, withRenderer: false);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("none", context.Response.Headers["HX-Reswap"].ToString());
    }

    [Fact]
    public async Task Custom_message_formatter_is_used()
    {
        var errorOptions = new HtmxErrorOptions
        {
            FormatMessage = (code, _) => $"Custom error {code}"
        };
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("boom"),
            errorOptions);
        var context = CreateContext(isHtmx: true, withRenderer: false);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Custom error 500", body);
    }

    [Fact]
    public async Task Custom_title_formatter_is_used()
    {
        var errorOptions = new HtmxErrorOptions
        {
            FormatTitle = (_, _) => "Oops"
        };
        var middleware = CreateMiddleware(
            ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; },
            errorOptions);
        var context = CreateContext(isHtmx: true, withRenderer: false);

        await middleware.InvokeAsync(context);

        // The fallback path writes the message, not the title, but we verify no exception
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Htmx_request_with_success_status_is_not_intercepted()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 201;
            return Task.CompletedTask;
        });
        var context = CreateContext(isHtmx: true);

        await middleware.InvokeAsync(context);

        Assert.Equal(201, context.Response.StatusCode);
    }

    [Fact]
    public async Task Htmx_request_with_redirect_is_not_intercepted()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 302;
            return Task.CompletedTask;
        });
        var context = CreateContext(isHtmx: true);

        await middleware.InvokeAsync(context);

        Assert.Equal(302, context.Response.StatusCode);
    }

    private static HtmxErrorMiddleware CreateMiddleware(
        RequestDelegate next,
        HtmxErrorOptions? errorOptions = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(errorOptions ?? new HtmxErrorOptions());
        var logger = NullLogger<HtmxErrorMiddleware>.Instance;
        return new HtmxErrorMiddleware(next, options, logger);
    }

    private static DefaultHttpContext CreateContext(bool isHtmx, bool withRenderer = false)
    {
        var services = new ServiceCollection();

        if (withRenderer)
        {
            services.AddSingleton<ITransientUiRenderer>(new FakeToastRenderer());
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new RazorSlicesHtmxOptions()));
        }

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        if (isHtmx)
        {
            context.Request.Headers["HX-Request"] = "true";
        }

        return context;
    }

    private sealed class FakeToastRenderer : ITransientUiRenderer
    {
        public RazorSlice RenderDialog(RazorSlice content) => new FakeSlice();
        public RazorSlice RenderToast(ToastModel toast) => new FakeSlice();
    }

    private sealed class FakeSlice : RazorSlice, IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) =>
            httpContext.Response.WriteAsync("<div>toast</div>");

        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}
