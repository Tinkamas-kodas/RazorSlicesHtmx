using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class FeatureResultBuilderTests
{
    private static readonly NavigationRouteItem TestRouteItem = new("test", "Test", "/test",
        new PageDefinition(() => null!));

    [Fact]
    public async Task BuildAsync_returns_full_page_for_non_htmx_request()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: false);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail).BuildAsync();

        Assert.True(fakeRenderer.PageRendered);
    }

    [Fact]
    public async Task BuildAsync_returns_fragment_for_htmx_request()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail).BuildAsync();

        Assert.False(fakeRenderer.PageRendered);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task BuildAsync_with_AsFragment_uses_fragment_as_primary()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();
        var fragment = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail)
            .AsFragment(fragment)
            .BuildAsync();

        // When AsFragment is set, navigation OOB should NOT be added
        Assert.False(fakeRenderer.NavigationRendered);
    }

    [Fact]
    public async Task BuildAsync_without_AsFragment_includes_navigation_oob()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail).BuildAsync();

        Assert.True(fakeRenderer.NavigationRendered);
    }

    [Fact]
    public async Task BuildAsync_non_htmx_with_NavigationUrl_redirects()
    {
        var (builder, _) = CreateBuilder(isHtmx: false);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail)
            .WithNavigation("/items")
            .BuildAsync();

        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task BuildAsync_non_htmx_with_LocationUrl_redirects()
    {
        var (builder, _) = CreateBuilder(isHtmx: false);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail)
            .WithLocation("/items")
            .BuildAsync();

        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task BuildAsync_htmx_with_NavigationUrl_sets_HX_Replace_Url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail)
            .AsFragment(new FakeSlice())
            .WithNavigation("/items", replace: true)
            .BuildAsync();

        var context = CreateHttpContext(isHtmx: true);
        await result.ExecuteAsync(context);

        Assert.Equal("/items", context.Response.Headers["HX-Replace-Url"].ToString());
    }

    [Fact]
    public async Task BuildAsync_htmx_with_NavigationUrl_push_sets_HX_Push_Url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail)
            .AsFragment(new FakeSlice())
            .WithNavigation("/items", replace: false)
            .BuildAsync();

        var context = CreateHttpContext(isHtmx: true);
        await result.ExecuteAsync(context);

        Assert.Equal("/items", context.Response.Headers["HX-Push-Url"].ToString());
    }

    [Fact]
    public async Task BuildAsync_htmx_with_LocationUrl_sets_HX_Location()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = await builder.Create(TestRouteItem, detail)
            .WithLocation("/items")
            .BuildAsync();

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
            builder.Create(TestRouteItem, new FakeSlice()).WithTrigger(""));
    }

    [Fact]
    public void WithNavigation_throws_on_empty_url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestRouteItem, new FakeSlice()).WithNavigation(""));
    }

    [Fact]
    public void WithLocation_throws_on_empty_url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestRouteItem, new FakeSlice()).WithLocation(""));
    }

    [Fact]
    public async Task WithToast_adds_toast_to_result()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var b = builder.Create(TestRouteItem, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithToast("Item saved", "Success", ToastTone.Success);

        var result = await b.BuildAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WithDialog_adds_dialog_oob()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var b = builder.Create(TestRouteItem, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithDialog(new FakeSlice());

        var result = await b.BuildAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ClearDialog_adds_empty_dialog_oob()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var b = builder.Create(TestRouteItem, new FakeSlice())
            .AsFragment(new FakeSlice())
            .ClearDialog();

        var result = await b.BuildAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WithState_adds_serialized_oob()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var state = new FakeState();

        var b = builder.Create(TestRouteItem, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithState(state);

        var result = await b.BuildAsync();
        Assert.NotNull(result);
        Assert.True(state.SerializeOobCalled);
    }

    [Fact]
    public void WithState_throws_on_null()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentNullException>(() =>
            builder.Create(TestRouteItem, new FakeSlice()).WithState(null!));
    }

    [Fact]
    public void WithRetarget_throws_on_empty_selector()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestRouteItem, new FakeSlice()).WithRetarget(""));
    }

    [Fact]
    public void WithReswap_throws_on_empty_mode()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestRouteItem, new FakeSlice()).WithReswap(""));
    }

    [Fact]
    public async Task BuildAsync_htmx_with_retarget_and_reswap_sets_headers()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var result = await builder.Create(TestRouteItem, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithRetarget("#edit-form")
            .WithReswap("outerHTML")
            .BuildAsync();

        var context = CreateHttpContext(isHtmx: true);
        await result.ExecuteAsync(context);

        Assert.Equal("#edit-form", context.Response.Headers["HX-Retarget"].ToString());
        Assert.Equal("outerHTML", context.Response.Headers["HX-Reswap"].ToString());
    }

    private static (FeatureResultBuilder builder, FakePageRenderer renderer) CreateBuilder(bool isHtmx)
    {
        var httpContext = CreateHttpContext(isHtmx);
        var httpContextAccessor = new FakeHttpContextAccessor(httpContext);
        var renderer = new FakePageRenderer();
        var transientUi = new FakeTransientUiRenderer();
        var options = Microsoft.Extensions.Options.Options.Create(new RazorSlicesHtmxOptions());

        var services = new ServiceCollection();
        services.AddSingleton<IFeaturePageRenderer>(renderer);
        services.AddSingleton<ITransientUiRenderer>(transientUi);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new RazorSlicesHtmxOptions()));

        var registry = FeatureRegistry.Discover(typeof(FeatureRegistryTests).Assembly, services.BuildServiceProvider());
        services.AddSingleton(registry);

        var sp = services.BuildServiceProvider();
        httpContext.RequestServices = sp;

        var builder = new FeatureResultBuilder(httpContextAccessor, sp, renderer, transientUi, options);
        return (builder, renderer);
    }

    private static DefaultHttpContext CreateHttpContext(bool isHtmx)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (isHtmx)
        {
            context.Request.Headers["HX-Request"] = "true";
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
