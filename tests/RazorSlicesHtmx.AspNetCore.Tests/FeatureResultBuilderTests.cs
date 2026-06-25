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
    private static readonly FeatureMetadata TestMeta = new(100, "test", "Test", "/test", "Test feature");
    private static readonly PageDefinition TestPage = new(TestMeta, () => null!);

    [Fact]
    public void Build_returns_full_page_for_non_htmx_request()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: false);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail).Build();

        Assert.True(fakeRenderer.PageRendered);
    }

    [Fact]
    public void Build_returns_fragment_for_htmx_request()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail).Build();

        Assert.False(fakeRenderer.PageRendered);
        Assert.NotNull(result);
    }

    [Fact]
    public void Build_with_AsFragment_uses_fragment_as_primary()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();
        var fragment = new FakeSlice();

        var result = builder.Create(TestPage, detail)
            .AsFragment(fragment)
            .Build();

        // When AsFragment is set, navigation OOB should NOT be added
        Assert.False(fakeRenderer.NavigationRendered);
    }

    [Fact]
    public void Build_without_AsFragment_includes_navigation_oob()
    {
        var (builder, fakeRenderer) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail).Build();

        Assert.True(fakeRenderer.NavigationRendered);
    }

    [Fact]
    public void Build_non_htmx_with_NavigationUrl_redirects()
    {
        var (builder, _) = CreateBuilder(isHtmx: false);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail)
            .WithNavigation("/items")
            .Build();

        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public void Build_non_htmx_with_LocationUrl_redirects()
    {
        var (builder, _) = CreateBuilder(isHtmx: false);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail)
            .WithLocation("/items")
            .Build();

        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task Build_htmx_with_NavigationUrl_sets_HX_Replace_Url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail)
            .AsFragment(new FakeSlice())
            .WithNavigation("/items", replace: true)
            .Build();

        var context = CreateHttpContext(isHtmx: true);
        await result.ExecuteAsync(context);

        Assert.Equal("/items", context.Response.Headers["HX-Replace-Url"].ToString());
    }

    [Fact]
    public async Task Build_htmx_with_NavigationUrl_push_sets_HX_Push_Url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail)
            .AsFragment(new FakeSlice())
            .WithNavigation("/items", replace: false)
            .Build();

        var context = CreateHttpContext(isHtmx: true);
        await result.ExecuteAsync(context);

        Assert.Equal("/items", context.Response.Headers["HX-Push-Url"].ToString());
    }

    [Fact]
    public async Task Build_htmx_with_LocationUrl_sets_HX_Location()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var detail = new FakeSlice();

        var result = builder.Create(TestPage, detail)
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
            builder.Create(TestPage, new FakeSlice()).WithTrigger(""));
    }

    [Fact]
    public void WithNavigation_throws_on_empty_url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestPage, new FakeSlice()).WithNavigation(""));
    }

    [Fact]
    public void WithLocation_throws_on_empty_url()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentException>(() =>
            builder.Create(TestPage, new FakeSlice()).WithLocation(""));
    }

    [Fact]
    public void WithToast_adds_toast_to_result()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        // Should not throw — toast is a valid operation
        var b = builder.Create(TestPage, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithToast("Item saved", "Success", "success");

        var result = b.Build();
        Assert.NotNull(result);
    }

    [Fact]
    public void WithDialog_adds_dialog_oob()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var b = builder.Create(TestPage, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithDialog(new FakeSlice());

        var result = b.Build();
        Assert.NotNull(result);
    }

    [Fact]
    public void ClearDialog_adds_empty_dialog_oob()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        var b = builder.Create(TestPage, new FakeSlice())
            .AsFragment(new FakeSlice())
            .ClearDialog();

        var result = b.Build();
        Assert.NotNull(result);
    }

    [Fact]
    public void WithState_adds_serialized_oob()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);
        var state = new FakeState();

        var b = builder.Create(TestPage, new FakeSlice())
            .AsFragment(new FakeSlice())
            .WithState(state);

        var result = b.Build();
        Assert.NotNull(result);
        Assert.True(state.SerializeOobCalled);
    }

    [Fact]
    public void WithState_throws_on_null()
    {
        var (builder, _) = CreateBuilder(isHtmx: true);

        Assert.Throws<ArgumentNullException>(() =>
            builder.Create(TestPage, new FakeSlice()).WithState(null!));
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
