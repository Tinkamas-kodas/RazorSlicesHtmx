using Microsoft.AspNetCore.Html;
using RazorSlicesHtmx.AspNetCore.Html;
using RazorSlicesHtmx.AspNetCore.HtmlNames;
using RazorSlicesHtmx.Bootstrap5.Options;

namespace RazorSlicesHtmx.Bootstrap5.Extensions;

/// <summary>
/// Bootstrap 5 form field helpers for <see cref="FieldContext{T}"/>.
/// All methods use <see cref="BootstrapFieldOptions.Current"/> when no explicit options are passed.
/// </summary>
public static class BootstrapFieldContextExtensions
{
    public static FluentTagBuilder Label<TValue>(this FieldContext<TValue> field, string text,
        BootstrapFieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var o = options ?? BootstrapFieldOptions.Current;

        return new FluentTagBuilder("label")
            .WithFor(field.Id)
            .WithClass(o.LabelClass)
            .WithContent(text);
    }

    /// <summary>
    /// Renders a label for a checkbox field using <c>CheckboxLabelClass</c>.
    /// </summary>
    public static FluentTagBuilder Label(this FieldContext<bool> field, string text,
        BootstrapFieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var o = options ?? BootstrapFieldOptions.Current;

        return new FluentTagBuilder("label")
            .WithFor(field.Id)
            .WithClass(o.CheckboxLabelClass)
            .WithContent(text);
    }

    public static FluentTagBuilder Hidden<TValue>(this FieldContext<TValue> field)
    {
        return new FluentTagBuilder("input")
            .WithAttribute("type", "hidden")
            .WithName(field.Name)
            .WithValue(field.Value?.ToString());
    }

    public static FluentTagBuilder Input<TValue>(this FieldContext<TValue> field,
        string type = "text",
        string? placeholder = null,
        bool autoFocus = false,
        bool required = false,
        bool disabled = false,
        bool readOnly = false,
        BootstrapFieldOptions? options = null)
    {
        var o = options ?? BootstrapFieldOptions.Current;
        var cssClass = field.HasErrors ? o.InputErrorClass : o.InputClass;

        return new FluentTagBuilder("input")
            .WithAttribute("type", type)
            .WithId(field.Id)
            .WithName(field.Name)
            .WithClass(cssClass)
            .WithValue(field.Value?.ToString())
            .WithPlaceholder(placeholder)
            .WithBooleanAttribute("autofocus", autoFocus)
            .WithBooleanAttribute("required", required)
            .WithBooleanAttribute("disabled", disabled)
            .WithBooleanAttribute("readonly", readOnly);
    }

    public static FluentTagBuilder TextArea<TValue>(this FieldContext<TValue> field,
        string? placeholder = null,
        int rows = 3,
        bool required = false,
        bool disabled = false,
        BootstrapFieldOptions? options = null)
    {
        var o = options ?? BootstrapFieldOptions.Current;
        var cssClass = field.HasErrors ? o.TextAreaErrorClass : o.TextAreaClass;

        return new FluentTagBuilder("textarea")
            .WithId(field.Id)
            .WithName(field.Name)
            .WithClass(cssClass)
            .WithAttribute("rows", rows.ToString())
            .WithPlaceholder(placeholder)
            .WithBooleanAttribute("required", required)
            .WithBooleanAttribute("disabled", disabled)
            .WithContent(field.Value?.ToString() ?? string.Empty);
    }

    public static IHtmlContent Checkbox(this FieldContext<bool> field,
        bool disabled = false,
        BootstrapFieldOptions? options = null)
    {
        var o = options ?? BootstrapFieldOptions.Current;

        var checkbox = new FluentTagBuilder("input")
            .WithAttribute("type", "checkbox")
            .WithId(field.Id)
            .WithClass(o.CheckboxClass)
            .WithValue("true")
            .WithAttribute("hx-on:change", "this.nextElementSibling.value = this.checked ? 'true' : 'false'")
            .WithBooleanAttribute("checked", field.Value)
            .WithBooleanAttribute("disabled", disabled);

        var hidden = new FluentTagBuilder("input")
            .WithAttribute("type", "hidden")
            .WithId($"{field.Id}_hidden")
            .WithName(field.Name)
            .WithValue(field.Value ? "true" : "false");

        return new HtmlContentBuilder()
            .AppendHtml(checkbox)
            .AppendHtml(hidden);
    }

    public static FluentTagBuilder Select<TValue>(this FieldContext<TValue> field,
        IEnumerable<(string Value, string Text)> options,
        string? emptyOption = null,
        bool required = false,
        bool disabled = false,
        BootstrapFieldOptions? fieldOptions = null)
    {
        var o = fieldOptions ?? BootstrapFieldOptions.Current;
        var cssClass = field.HasErrors ? o.SelectErrorClass : o.SelectClass;
        var currentValue = field.Value?.ToString();

        var builder = new FluentTagBuilder("select")
            .WithId(field.Id)
            .WithName(field.Name)
            .WithClass(cssClass)
            .WithBooleanAttribute("required", required)
            .WithBooleanAttribute("disabled", disabled);

        if (emptyOption is not null)
        {
            builder.WithContent(
                new FluentTagBuilder("option").WithValue("").WithContent(emptyOption)
            );
        }

        foreach (var (value, text) in options)
        {
            var selected = string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase);
            builder.WithContent(
                new FluentTagBuilder("option")
                    .WithValue(value)
                    .WithBooleanAttribute("selected", selected)
                    .WithContent(text)
            );
        }

        return builder;
    }

    public static IHtmlContent Validation<TValue>(this FieldContext<TValue> field,
        BootstrapFieldOptions? options = null)
    {
        var errors = field.FieldErrors;
        if (errors.Length == 0)
        {
            return HtmlString.Empty;
        }

        var o = options ?? BootstrapFieldOptions.Current;

        var content = new HtmlContentBuilder(errors.Length * 3);
        foreach (var (_, errorMessage) in errors)
        {
            content.AppendHtml("<div class=\"");
            content.Append(o.ValidationClass);
            content.AppendHtml("\">");
            content.Append(errorMessage);
            content.AppendHtml("</div>");
        }

        return content;
    }
}
