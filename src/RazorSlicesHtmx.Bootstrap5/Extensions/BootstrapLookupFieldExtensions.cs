using Microsoft.AspNetCore.Html;
using RazorSlicesHtmx.AspNetCore.Html;
using RazorSlicesHtmx.Bootstrap5.Options;

namespace RazorSlicesHtmx.Bootstrap5.Extensions;

/// <summary>
/// Bootstrap 5 lookup field helpers for searchable HTMX-backed pickers.
/// </summary>
public static class BootstrapLookupFieldExtensions
{
    public static IHtmlContent LookupSelect<TValue>(
        this FieldContext<TValue> field,
        string path,
        string selectedValueTextFieldName,
        string selectedValueResolvePath,
        string? placeholder = null,
        string? selectedText = null,
        int pageSize = 10,
        bool disabled = false,
        BootstrapFieldOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedValueTextFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedValueResolvePath);

        var o = options ?? BootstrapFieldOptions.Current;
        var inputCss = field.HasErrors ? o.InputErrorClass : o.InputClass;
        var searchId = $"{field.Id}_lookup_search";
        var clearId = $"{field.Id}_lookup_clear";
        var textHiddenId = $"{field.Id}_lookup_text";
        var initialValue = field.Value?.ToString();
        var hasInitialValue = !string.IsNullOrWhiteSpace(initialValue);
        var initialText = string.IsNullOrWhiteSpace(selectedText) ? string.Empty : selectedText;

        var wrapper = new FluentTagBuilder("rsh-lookup-select")
            .WithClass("rsh-lookup-select d-block")
            .WithAttribute("data-hidden-id", field.Id)
            .WithAttribute("data-search-id", searchId)
            .WithAttribute("data-clear-id", clearId)
            .WithAttribute("data-text-hidden-id", textHiddenId)
            .WithAttribute("data-resolve-path", selectedValueResolvePath)
            .WithAttribute("data-path", path)
            .WithAttribute("data-component-disabled", disabled ? "true" : "false")
            .WithAttribute("data-page-size", pageSize.ToString());

        var hiddenInput = new FluentTagBuilder("input")
            .WithAttribute("type", "hidden")
            .WithId(field.Id)
            .WithName(field.Name)
            .WithValue(initialValue)
            .WithBooleanAttribute("disabled", disabled || !hasInitialValue);

        var textHiddenInput = new FluentTagBuilder("input")
            .WithAttribute("type", "hidden")
            .WithId(textHiddenId)
            .WithName(selectedValueTextFieldName)
            .WithValue(initialText)
            .WithBooleanAttribute("disabled", disabled || !hasInitialValue);

        var searchInput = new FluentTagBuilder("input")
            .WithAttribute("type", "search")
            .WithId(searchId)
            .WithClass(inputCss)
            .WithPlaceholder(placeholder)
            .WithValue(initialText)
            .WithAttribute("autocomplete", "off")
            .WithBooleanAttribute("disabled", disabled)
            .WithAttribute("hx-get", path)
            .WithAttribute("hx-target", "next .rsh-lookup-results")
            .WithAttribute("hx-swap", "innerHTML")
            .WithAttribute("hx-trigger", "focus, input changed delay:250ms, search")
            .WithAttribute(
                "hx-vals",
                $"js:{{ query: this.value, page: 1, pageSize: {pageSize}, path: '{EscapeJs(path)}' }}");

        var clearButton = new FluentTagBuilder("button")
            .WithAttribute("type", "button")
            .WithId(clearId)
            .WithClass("btn btn-outline-secondary")
            .WithBooleanAttribute("disabled", disabled)
            .WithAttribute("aria-label", "Clear selection")
            .WithContent("Clear");

        var results = new FluentTagBuilder("div")
            .WithClass("rsh-lookup-results list-group shadow-sm d-none")
            .WithAttribute("role", "listbox")
            .WithAttribute("aria-label", "Lookup results");

        var searchSurface = new FluentTagBuilder("div")
            .WithClass("rsh-lookup-search-surface position-relative flex-grow-1")
            .WithContent(searchInput)
            .WithContent(results);

        var inputGroup = new FluentTagBuilder("div")
            .WithClass("input-group")
            .WithContent(searchSurface)
            .WithContent(clearButton);

        var surface = new FluentTagBuilder("div")
            .WithClass("rsh-lookup-surface position-relative")
            .WithContent(inputGroup);

        return wrapper
            .WithContent(hiddenInput)
            .WithContent(textHiddenInput)
            .WithContent(surface);
    }

    private static string EscapeJs(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
    }
}
