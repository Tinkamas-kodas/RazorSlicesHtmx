using Microsoft.AspNetCore.Http;
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.AspNetCore.Htmx;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class HtmlHelperExtensionsTests
{
    [Theory]
    [InlineData("clients-list", "clients-list")]
    [InlineData("#clients-list", "clients-list")]
    [InlineData("section#clients-list", "clients-list")]
    [InlineData("section.clients-panel#clients-list", "clients-list")]
    public void IsHtmxTarget_matches_plain_hash_and_css_selector_target_formats(string headerValue, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Hx.Target] = headerValue;

        var result = context.IsHtmxTarget(expected);

        Assert.True(result);
    }

    [Fact]
    public void IsHtmxTarget_returns_false_for_different_target()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Hx.Target] = "section#issuers-list";

        var result = context.IsHtmxTarget("clients-list");

        Assert.False(result);
    }
}
