using Microsoft.AspNetCore.Html;
using RazorSlicesHtmx.Bootstrap5.Options;
using RazorSlicesHtmx.HtmlNames;

namespace RazorSlicesHtmx.Bootstrap5.Extensions;

/// <summary>
/// Bootstrap 5 form field helpers for <see cref="FieldContext{T}"/>.
/// All methods use <see cref="BootstrapFieldOptions.Current"/> when no explicit options are passed.
/// </summary>
public static class BootstrapFieldContextExtensions
{
    public static IHtmlContent Label<TValue>(this FieldContext<TValue> field, string text,
        BootstrapFieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var o = options ?? BootstrapFieldOptions.Current;

        var content = new HtmlContentBuilder(5);
        content.AppendHtml("<label for=\"");
        content.Append(field.Id);
        content.AppendHtml("\" class=\"");
        content.Append(o.LabelClass);
        content.AppendHtml("\">");
        content.Append(text);
        content.AppendHtml("</label>");
        return content;
    }

    public static IHtmlContent Input<TValue>(this FieldContext<TValue> field,
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

        var content = new HtmlContentBuilder(12);
        content.AppendHtml("<input type=\"");
        content.Append(type);
        content.AppendHtml("\" id=\"");
        content.Append(field.Id);
        content.AppendHtml("\" name=\"");
        content.Append(field.Name);
        content.AppendHtml("\" class=\"");
        content.Append(cssClass);
        content.AppendHtml("\"");

        if (field.Value is not null)
        {
            content.AppendHtml(" value=\"");
            content.Append(field.Value.ToString()!);
            content.AppendHtml("\"");
        }

        if (placeholder is not null)
        {
            content.AppendHtml(" placeholder=\"");
            content.Append(placeholder);
            content.AppendHtml("\"");
        }

        if (autoFocus) content.AppendHtml(" autofocus");
        if (required) content.AppendHtml(" required");
        if (disabled) content.AppendHtml(" disabled");
        if (readOnly) content.AppendHtml(" readonly");

        content.AppendHtml(">");
        return content;
    }

    public static IHtmlContent TextArea<TValue>(this FieldContext<TValue> field,
        string? placeholder = null,
        int rows = 3,
        bool required = false,
        bool disabled = false,
        BootstrapFieldOptions? options = null)
    {
        var o = options ?? BootstrapFieldOptions.Current;
        var cssClass = field.HasErrors ? o.TextAreaErrorClass : o.TextAreaClass;

        var content = new HtmlContentBuilder(8);
        content.AppendHtml("<textarea id=\"");
        content.Append(field.Id);
        content.AppendHtml("\" name=\"");
        content.Append(field.Name);
        content.AppendHtml("\" class=\"");
        content.Append(cssClass);
        content.AppendHtml("\" rows=\"");
        content.Append(rows.ToString());
        content.AppendHtml("\"");

        if (placeholder is not null)
        {
            content.AppendHtml(" placeholder=\"");
            content.Append(placeholder);
            content.AppendHtml("\"");
        }

        if (required) content.AppendHtml(" required");
        if (disabled) content.AppendHtml(" disabled");

        content.AppendHtml(">");
        if (field.Value is not null)
        {
            content.Append(field.Value.ToString()!);
        }

        content.AppendHtml("</textarea>");
        return content;
    }

    public static IHtmlContent Checkbox(this FieldContext<bool> field,
        string? label = null,
        bool disabled = false,
        BootstrapFieldOptions? options = null)
    {
        var o = options ?? BootstrapFieldOptions.Current;

        var content = new HtmlContentBuilder(8);
        content.AppendHtml("<div class=\"");
        content.Append(o.CheckboxWrapperClass);
        content.AppendHtml("\">");
        content.AppendHtml("<input type=\"checkbox\" id=\"");
        content.Append(field.Id);
        content.AppendHtml("\" name=\"");
        content.Append(field.Name);
        content.AppendHtml("\" class=\"");
        content.Append(o.CheckboxClass);
        content.AppendHtml("\" value=\"true\"");

        if (field.Value) content.AppendHtml(" checked");
        if (disabled) content.AppendHtml(" disabled");

        content.AppendHtml(">");

        if (label is not null)
        {
            content.AppendHtml("<label class=\"");
            content.Append(o.CheckboxLabelClass);
            content.AppendHtml("\" for=\"");
            content.Append(field.Id);
            content.AppendHtml("\">");
            content.Append(label);
            content.AppendHtml("</label>");
        }

        content.AppendHtml("</div>");
        return content;
    }

    public static IHtmlContent Select<TValue>(this FieldContext<TValue> field,
        IEnumerable<(string Value, string Text)> options,
        string? emptyOption = null,
        bool required = false,
        bool disabled = false,
        BootstrapFieldOptions? fieldOptions = null)
    {
        var o = fieldOptions ?? BootstrapFieldOptions.Current;
        var cssClass = field.HasErrors ? o.SelectErrorClass : o.SelectClass;
        var currentValue = field.Value?.ToString();

        var content = new HtmlContentBuilder(16);
        content.AppendHtml("<select id=\"");
        content.Append(field.Id);
        content.AppendHtml("\" name=\"");
        content.Append(field.Name);
        content.AppendHtml("\" class=\"");
        content.Append(cssClass);
        content.AppendHtml("\"");

        if (required) content.AppendHtml(" required");
        if (disabled) content.AppendHtml(" disabled");

        content.AppendHtml(">");

        if (emptyOption is not null)
        {
            content.AppendHtml("<option value=\"\">");
            content.Append(emptyOption);
            content.AppendHtml("</option>");
        }

        foreach (var (value, text) in options)
        {
            var selected = string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase);
            if (selected)
            {
                content.AppendHtml("<option value=\"");
                content.Append(value);
                content.AppendHtml("\" selected>");
            }
            else
            {
                content.AppendHtml("<option value=\"");
                content.Append(value);
                content.AppendHtml("\">");
            }

            content.Append(text);
            content.AppendHtml("</option>");
        }

        content.AppendHtml("</select>");
        return content;
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
