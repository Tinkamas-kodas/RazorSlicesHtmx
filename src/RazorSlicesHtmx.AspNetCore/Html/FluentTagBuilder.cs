using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RazorSlicesHtmx.AspNetCore.Html;

public class FluentTagBuilder(string tagName) : IHtmlContent
{
    private readonly TagBuilder _tagBuilder = new(tagName);

    public FluentTagBuilder WithId(string id)
    {
        _tagBuilder.Attributes["id"] = id;
        return this;
    }

    public FluentTagBuilder WithName(string name)
    {
        _tagBuilder.Attributes["name"] = name;
        return this;
    }

    public FluentTagBuilder WithValue(string? value)
    {
        if (value is not null)
        {
            _tagBuilder.Attributes["value"] = value;
        }
        return this;
    }

    public FluentTagBuilder WithFor(string id)
    {
        _tagBuilder.Attributes["for"] = id;
        return this;
    }

    public FluentTagBuilder WithClass(string? classes, bool condition = true)
    {
        if (condition && !string.IsNullOrWhiteSpace(classes))
        {
            _tagBuilder.AddCssClass(classes);
        }
        return this;
    }

    public FluentTagBuilder WithPlaceholder(string? placeholder)
    {
        if (placeholder is not null)
        {
            _tagBuilder.Attributes["placeholder"] = placeholder;
        }
        return this;
    }

    public FluentTagBuilder WithContent(string content)
    {
        _tagBuilder.InnerHtml.Append(content);
        return this;
    }

    public FluentTagBuilder WithContent(IHtmlContent content)
    {
        _tagBuilder.InnerHtml.AppendHtml(content);
        return this;
    }

    public FluentTagBuilder WithAttribute(string name, string? value, bool condition = true)
    {
        if (condition && value is not null)
        {
            _tagBuilder.Attributes[name] = value;
        }
        return this;
    }

    public FluentTagBuilder WithBooleanAttribute(string name, bool condition = true)
    {
        if (condition)
        {
            _tagBuilder.Attributes[name] = name;
        }
        return this;
    }

    public void WriteTo(TextWriter writer, HtmlEncoder encoder)
    {
        _tagBuilder.WriteTo(writer, encoder);
    }
}
