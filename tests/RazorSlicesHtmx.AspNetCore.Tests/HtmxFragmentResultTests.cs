using Microsoft.AspNetCore.Http;
using RazorSlicesHtmx.AspNetCore.Htmx;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class HtmxFragmentResultTests
{
    [Fact]
    public void WithOob_throws_on_empty_selector()
    {
        var builder = HtmxFragmentResult.Create(null!);

        Assert.Throws<ArgumentException>(() => builder.WithOob("", null!));
        Assert.Throws<ArgumentException>(() => builder.WithOob("  ", null!));
    }

    [Fact]
    public void WithTrigger_throws_on_empty_name()
    {
        var builder = HtmxFragmentResult.Create(null!);

        Assert.Throws<ArgumentException>(() => builder.WithTrigger(""));
        Assert.Throws<ArgumentException>(() => builder.WithTrigger("  "));
    }

    [Fact]
    public async Task Build_with_triggers_sets_HX_Trigger_header()
    {
        var builder = HtmxFragmentResult.Create(new FakeSlice());

        builder.WithTrigger("item-saved");
        builder.WithTrigger("list-refresh");

        var result = builder.Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        Assert.True(httpContext.Response.Headers.ContainsKey("HX-Trigger"));
        var trigger = httpContext.Response.Headers["HX-Trigger"].ToString();
        Assert.Contains("item-saved", trigger);
        Assert.Contains("list-refresh", trigger);
    }

    [Fact]
    public async Task Build_without_triggers_does_not_set_HX_Trigger()
    {
        var builder = HtmxFragmentResult.Create(new FakeSlice());

        var result = builder.Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        Assert.False(httpContext.Response.Headers.ContainsKey("HX-Trigger"));
    }

    [Fact]
    public void Build_without_oob_returns_primary_directly()
    {
        var primary = new FakeSlice();
        var builder = HtmxFragmentResult.Create(primary);

        var result = builder.Build();

        Assert.Same(primary, result);
    }

    [Fact]
    public void WithOob_with_default_swap_uses_innerHTML()
    {
        var builder = HtmxFragmentResult.Create(new FakeSlice());

        // Should not throw — uses default HtmxSwap.InnerHtml
        builder.WithOob("#target", new FakeSlice());

        var result = builder.Build();

        // Result should be an HtmxFragment (not the primary directly) because OOB was added
        Assert.IsNotType<FakeSlice>(result);
    }

    /// <summary>
    /// Minimal IResult implementation for testing builder logic without Razor rendering.
    /// </summary>
    private sealed class FakeSlice : RazorSlices.RazorSlice, IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            return httpContext.Response.WriteAsync("<div>fake</div>");
        }

        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }
}
