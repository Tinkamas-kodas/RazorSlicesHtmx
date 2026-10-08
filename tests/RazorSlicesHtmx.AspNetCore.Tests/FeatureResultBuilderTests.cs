using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class FeatureResultBuilderTests
{
    private static readonly NavigationItem TestNavItem = new("test", "Test", "/test");

    [Fact]
    public Task BuildAsync_returns_full_page_for_non_htmx_request()
    {
        try
        {
            var (builder, fakeRenderer) = CreateBuilder(isHtmx: false);
            var detail = new FakeSlice();

            builder.Create(TestNavItem, detail).Build();

            Assert.True(fakeRenderer.PageRendered);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task BuildAsync_returns_fragment_for_htmx_request()
    {
        try
        {
            var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
            var detail = new FakeSlice();

            var result = builder.Create(TestNavItem, detail).Build();

            Assert.False(fakeRenderer.PageRendered);
            Assert.NotNull(result);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task BuildAsync_htmx_same_feature_url_does_not_include_navigation_oob()
    {
        try
        {
            // Same-feature HTMX request: HX-Current-URL points to the same route → nav OOB not sent
            var (builder, fakeRenderer) = CreateBuilder(isHtmx: true, hxCurrentUrl: "http://localhost/test");
            var detail = new FakeSlice();

            builder.Create(TestNavItem, detail).Build();

            // Same feature → navigation OOB should NOT be added
            Assert.False(fakeRenderer.NavigationRendered);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task BuildAsync_without_AsFragment_includes_navigation_oob()
    {
        try
        {
            var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
            var detail = new FakeSlice();

            builder.Create(TestNavItem, detail).Build();

            Assert.True(fakeRenderer.NavigationRendered);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task BuildAsync_non_htmx_with_NavigationUrl_redirects()
    {
        try
        {
            var (builder, _) = CreateBuilder(isHtmx: false);
            var detail = new FakeSlice();

            var result = builder.Create(TestNavItem, detail)
                .WithNavigation("/items")
                .Build();

            Assert.IsAssignableFrom<IResult>(result);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task BuildAsync_non_htmx_with_LocationUrl_redirects()
    {
        try
        {
            var (builder, _) = CreateBuilder(isHtmx: false);
            var detail = new FakeSlice();

            var result = builder.Create(TestNavItem, detail)
                .WithLocation("/items")
                .Build();

            Assert.IsAssignableFrom<IResult>(result);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public async Task BuildAsync_htmx_with_NavigationUrl_sets_HX_Replace_Url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestNavItem, detail)
            .WithNavigation("/items", replace: true)
            .Build();

        var context = builder.Request.HttpContext;
        await result.ExecuteAsync(context);

        Assert.Equal("/items", context.Response.Headers["HX-Replace-Url"].ToString());
    }

    [Fact]
    public async Task BuildAsync_htmx_with_NavigationUrl_push_sets_HX_Push_Url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestNavItem, detail)
            .WithNavigation("/items", replace: false)
            .Build();

        var context = builder.Request.HttpContext;
        await result.ExecuteAsync(context);

        Assert.Equal("/items", context.Response.Headers["HX-Push-Url"].ToString());
    }

    [Fact]
    public async Task BuildAsync_htmx_with_LocationUrl_sets_HX_Location()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestNavItem, detail)
            .WithLocation("/items")
            .Build();

        var context = CreateHttpContext(isHtmx: true);
        await result.ExecuteAsync(context);

        Assert.True(context.Response.Headers.ContainsKey("HX-Location"));
        var payload = context.Response.Headers["HX-Location"].ToString();
        Assert.Contains("/items", payload);
    }

    [Fact]
    public void WithTrigger_throws_on_empty_name()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestNavItem, new FakeSlice()).WithTrigger(""));
    }

    [Fact]
    public void WithNavigation_throws_on_empty_url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestNavItem, new FakeSlice()).WithNavigation(""));
    }

    [Fact]
    public void WithLocation_throws_on_empty_url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestNavItem, new FakeSlice()).WithLocation(""));
    }

    [Fact]
    public Task WithToast_adds_toast_to_result()
    {
        try
        {
            var (builder, _) = CreateBuilder(isHtmx: true);

            var b = builder.Create(TestNavItem, new FakeSlice())
                .WithToast("Item saved", "Success");

            var result = b.Build();
            Assert.NotNull(result);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task WithDialog_adds_dialog_oob()
    {
        try
        {
            var (builder, _) = CreateBuilder(isHtmx: true);

            var b = builder.Create(TestNavItem, new FakeSlice())
                .WithDialog(new FakeSlice());

            var result = b.Build();
            Assert.NotNull(result);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task ClearDialog_adds_empty_dialog_oob()
    {
        try
        {
            var (builder, _) = CreateBuilder(isHtmx: true);

            var b = builder.Create(TestNavItem, new FakeSlice())
                .ClearDialog();

            var result = b.Build();
            Assert.NotNull(result);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task WithState_adds_serialized_oob()
    {
        try
        {
            var (builder, _) = CreateBuilder(isHtmx: true);
            var state = new FakeState();

            var b = builder.Create(TestNavItem, new FakeSlice())
                .WithState(state);

            var result = b.Build();
            Assert.NotNull(result);
            Assert.True(state.SerializeOobCalled);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public void WithState_throws_on_null()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentNullException>(() =>
            builder.Create(TestNavItem, new FakeSlice()).WithState(null!));
    }

    [Fact]
    public void WithRetarget_throws_on_empty_selector()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestNavItem, new FakeSlice()).WithRetarget(""));
    }

    [Fact]
    public void WithReswap_throws_on_empty_mode()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestNavItem, new FakeSlice()).WithReswap(""));
    }

    [Fact]
    public async Task BuildAsync_htmx_with_retarget_and_reswap_sets_headers()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var result = builder.Create(TestNavItem, new FakeSlice())
            .WithNav(false)
            .WithRetarget("#edit-form")
            .WithReswap("outerHTML")
            .Build();

        var context = builder.Request.HttpContext;
        await result.ExecuteAsync(context);

        Assert.Equal("#edit-form", context.Response.Headers["HX-Retarget"].ToString());
        Assert.Equal("outerHTML", context.Response.Headers["HX-Reswap"].ToString());
    }

    private static (FeatureResultBuilder builder, FakePageRenderer renderer) CreateBuilder(bool isHtmx, string? hxCurrentUrl = null)
    {
        var httpContext = CreateHttpContext(isHtmx, hxCurrentUrl);
        var httpContextAccessor = new FakeHttpContextAccessor(httpContext);
        var renderer = new FakePageRenderer();
        var transientUi = new FakeTransientUiRenderer();
        var options = Microsoft.Extensions.Options.Options.Create(new RazorSlicesHtmxOptions());

        var services = new ServiceCollection();
        services.AddSingleton<IFeaturePageRenderer>(renderer);
        services.AddSingleton<ITransientUiRenderer>(transientUi);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new RazorSlicesHtmxOptions()));
        services.AddScoped<HtmxStateContext>();

        services
            .AddFeature<AlphaFeature>()
            .AddFeature<BravoFeature>()
            .AddFeature<CharlieFeature>();

        var registry = FeatureRegistry.FromRegistrations(services.BuildServiceProvider());
        services.AddSingleton(registry);
        services.AddSingleton<INavOobStrategy, HxCurrentUrlNavStrategy>();

        var sp = services.BuildServiceProvider();
        httpContext.RequestServices = sp;

        var builder = new FeatureResultBuilder(httpContextAccessor, sp, renderer, transientUi, options, sp.GetRequiredService<INavOobStrategy>());
        return (builder, renderer);
    }

    private static DefaultHttpContext CreateHttpContext(bool isHtmx, string? hxCurrentUrl = null)
    {
        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        if (isHtmx)
        {
            context.Request.Headers["HX-Request"] = "true";
        }
        if (hxCurrentUrl is not null)
        {
            context.Request.Headers["HX-Current-URL"] = hxCurrentUrl;
        }
        return context;
    }

    private sealed class FakeHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private sealed class FakePageRenderer : IFeaturePageRenderer
    {
        public bool PageRendered { get; private set; }
        public bool NavigationRendered { get; private set; }

        public IResult RenderPage(FeatureShellContext context)
        {
            PageRendered = true;
            return Microsoft.AspNetCore.Http.Results.Ok("page");
        }

        public RazorSlice RenderNavigation(FeatureShellContext context)
        {
            NavigationRendered = true;
            return new FakeSlice();
        }
    }

    private sealed class FakeTransientUiRenderer : ITransientUiRenderer
    {
        public RazorSlice RenderDialog(RazorSlice content) => new FakeSlice();
        public RazorSlice RenderToast(ToastModel toast) => new FakeSlice();
    }

    private sealed class FakeState : IHasState
    {
        public bool SerializeOobCalled { get; private set; }

        public Microsoft.AspNetCore.Html.IHtmlContent Serialize() =>
            new Microsoft.AspNetCore.Html.HtmlString("<div>state</div>");

        public Microsoft.AspNetCore.Html.IHtmlContent SerializeOob()
        {
            SerializeOobCalled = true;
            return new Microsoft.AspNetCore.Html.HtmlString("<div hx-swap-oob=\"outerHTML\">state</div>");
        }
    }

    private sealed class FakeSlice : RazorSlice, IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) =>
            httpContext.Response.WriteAsync("<div>fake</div>");

        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}

