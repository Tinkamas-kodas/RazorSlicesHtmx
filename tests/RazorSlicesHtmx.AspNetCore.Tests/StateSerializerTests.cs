using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class StateSerializerTests
{
    [Fact]
    public void Serialize_renders_hidden_inputs_inside_div()
    {
        var fields = new[]
        {
            new StateFieldValue("Search", "hello"),
            new StateFieldValue("Page", "2")
        };

        var html = StateSerializer.Serialize("my-state", fields).ToString()!;

        Assert.StartsWith("<div id=\"my-state\">", html);
        Assert.EndsWith("</div>", html);
        Assert.Contains("<input type=\"hidden\" name=\"Search\" value=\"hello\">", html);
        Assert.Contains("<input type=\"hidden\" name=\"Page\" value=\"2\">", html);
    }

    [Fact]
    public void Serialize_with_empty_fields_renders_empty_div()
    {
        var html = StateSerializer.Serialize("empty-state", []).ToString()!;

        Assert.Equal("<div id=\"empty-state\"></div>", html);
    }

    [Fact]
    public void Serialize_html_encodes_values()
    {
        var fields = new[] { new StateFieldValue("q", "<script>alert(1)</script>") };

        var html = StateSerializer.Serialize("safe", fields).ToString()!;

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void SerializeOob_wraps_with_hx_swap_oob()
    {
        var fields = new[] { new StateFieldValue("Search", "test") };

        var html = StateSerializer.SerializeOob("my-state", fields).ToString()!;

        Assert.StartsWith("<div hx-swap-oob=\"outerHTML:#my-state\">", html);
        Assert.Contains("<div id=\"my-state\">", html);
        Assert.Contains("<input type=\"hidden\" name=\"Search\" value=\"test\">", html);
        Assert.EndsWith("</div></div>", html);
    }

    [Fact]
    public void StateFieldValue_FromObject_converts_null_to_empty_string()
    {
        var field = StateFieldValue.FromObject("Field", null);

        Assert.Equal("Field", field.Name);
        Assert.Equal(string.Empty, field.Value);
    }

    [Fact]
    public void StateFieldValue_FromObject_converts_int_to_invariant_string()
    {
        var field = StateFieldValue.FromObject("Count", 42);

        Assert.Equal("42", field.Value);
    }

    [Fact]
    public void StateFieldValue_FromObject_passes_string_through()
    {
        var field = StateFieldValue.FromObject("Name", "hello");

        Assert.Equal("hello", field.Value);
    }

    [Fact]
    public void StateFieldValue_FromObject_throws_on_empty_name()
    {
        Assert.Throws<ArgumentException>(() => StateFieldValue.FromObject("", "value"));
        Assert.Throws<ArgumentException>(() => StateFieldValue.FromObject("  ", "value"));
    }
}
