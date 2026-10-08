# Bootstrap5

`RazorSlicesHtmx.Bootstrap5` is a Bootstrap 5 adapter for transient UI. It is intentionally not a full app shell or admin template.

## Registration

```csharp
using RazorSlicesHtmx.Bootstrap5.Extensions;

builder.Services.AddRazorSlicesHtmxBootstrap5();
```

With options:

```csharp
builder.Services.AddRazorSlicesHtmxBootstrap5(options =>
{
    options.DialogClass = "modal-dialog modal-dialog-centered modal-lg";
    options.ToastDelayMilliseconds = 3200;
    options.FieldOptions = BootstrapFieldOptions.Default with
    {
        InputClass = "form-control form-control-sm",
        LabelClass = "form-label fw-bold"
    };
});
```

## What it registers

- `ITransientUiRenderer` — Bootstrap dialog and toast rendering
- `PostConfigure<RazorSlicesHtmxOptions>` — Bootstrap fallback defaults:
  - `DialogClass` → `modal-dialog modal-dialog-centered`
  - `ToastDelayMilliseconds` → `2600`

## Form field helpers

From `RazorSlicesHtmx.Bootstrap5.Extensions`:

```cshtml
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.Request.For(Model.Errors); }

@f.Code.Input(placeholder: "ITEM-001", autoFocus: true)
@f.Code.Label("Code")
@f.Code.Validation()

@f.Name.Input()
@f.Name.Label("Name")
@f.Name.Validation()

@f.Note.TextArea(placeholder: "Optional note", rows: 3)
@f.Note.Validation()

@f.IsActive.Checkbox()
@f.IsActive.Label("Active")

@f.Status.Select(statusOptions, emptyOption: "— Select —")
@f.Status.Validation()
```

> **Note:** For Bootstrap 5 [floating labels](https://getbootstrap.com/docs/5.3/forms/floating-labels/), wrap the field in `<div class="form-floating">` and place `Input` **before** `Label`.

### Fluent chaining

All helpers (except `.Validation()`) return `FluentTagBuilder` — an `IHtmlContent` builder that allows chaining additional HTML attributes:

```cshtml
@* Add extra CSS classes *@
@f.Code.Label("Code").WithClass("fw-bold")

@* Add arbitrary HTML attributes *@
@f.Name.Input(placeholder: "Name").WithAttribute("data-validate", "true")

@* Boolean (valueless) attributes *@
@f.Note.TextArea().WithBooleanAttribute("spellcheck")

@* Conditional class *@
@f.Code.Input().WithClass("border-primary", condition: Model.IsNew)
```

See [FluentTagBuilder](#fluenttagbuilder) for full method reference.

### Helper reference

| Helper | Return type | Notes |
|--------|-------------|-------|
| `.Label(text)` | `FluentTagBuilder` | Uses `CheckboxLabelClass` on `FieldContext<bool>` |
| `.Input(...)` | `FluentTagBuilder` | Adds `is-invalid` when field has errors |
| `.TextArea(...)` | `FluentTagBuilder` | |
| `.Select(options, ...)` | `FluentTagBuilder` | |
| `.Hidden()` | `FluentTagBuilder` | Renders `<input type="hidden">` |
| `.Checkbox(...)` | `FluentTagBuilder` | Chains apply to the checkbox `<input>`; hidden false input is always appended automatically via suffix |
| `.Validation()` | `IHtmlContent` | Renders `invalid-feedback` divs; empty when no errors |

All helpers accept an optional `BootstrapFieldOptions` to override CSS classes per-call.

## Lookup helper and web component

`RazorSlicesHtmx.Bootstrap5` includes a lookup helper (`LookupSelect`) and a browser custom element (`rsh-lookup-select`).

Helper usage:

```cshtml
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.Request.For(); }

@f.ClientId.LookupSelect(
    path: FeatureUri.Uri("search"),
    selectedValueTextFieldName: f.ClientName.Name,
    selectedValueResolvePath: FeatureUri.Uri("search-selected"),
    placeholder: "Type code or client name",
    selectedText: Model.Request.ClientName,
    pageSize: 12)
```

Without helper:

- Render `rsh-lookup-select` markup manually
- Keep `data-*` ID mapping consistent
- Render option items with `data-rsh-lookup-value` and `data-rsh-lookup-text`
- Hidden value/text inputs are disabled while selection is empty and automatically enabled on selection

Load component assets in layout:

```cshtml
<link rel="stylesheet" href="/_content/RazorSlicesHtmx.Bootstrap5/css/rsh-lookup-select.css">
<script src="/_content/RazorSlicesHtmx.Bootstrap5/js/rsh-lookup-select.js"></script>
```

Full usage examples (with helper and without helper) are in [Lookup](lookup.md).

## FluentTagBuilder

`FluentTagBuilder` is a fluent HTML element builder in `RazorSlicesHtmx.AspNetCore.Html`. It wraps ASP.NET Core's `TagBuilder` and implements `IHtmlContent`, so it can be rendered directly with `@` in Razor.

```cshtml
@using RazorSlicesHtmx.AspNetCore.Html

@(new FluentTagBuilder("span").WithClass("badge text-bg-primary").WithContent("New"))
```

### Method reference

| Method | Description |
|--------|-------------|
| `.WithId(id)` | Sets `id` attribute |
| `.WithName(name)` | Sets `name` attribute |
| `.WithFor(id)` | Sets `for` attribute (for `<label>`) |
| `.WithValue(value)` | Sets `value` attribute; no-op when `null` |
| `.WithClass(classes, condition?)` | Appends CSS class(es); skipped when `null`/empty or `condition` is `false` |
| `.WithPlaceholder(placeholder)` | Sets `placeholder` attribute; no-op when `null` |
| `.WithAttribute(name, value, condition?)` | Sets arbitrary attribute; no-op when `value` is `null` or `condition` is `false` |
| `.WithBooleanAttribute(name, condition?)` | Adds boolean/valueless attribute (e.g. `disabled`, `required`); skipped when `condition` is `false` |
| `.WithContent(string)` | Appends text content (HTML-encoded) |
| `.WithContent(IHtmlContent)` | Appends raw HTML content |
| `.WithSuffix(IHtmlContent)` | Appends sibling HTML rendered **after** the closing tag (used internally by `.Checkbox()` to emit the hidden input) |

### BootstrapFieldOptions

All CSS classes are configurable. Global defaults via `BootstrapFieldOptions.Default`:

```csharp
public record BootstrapFieldOptions
{
    public string InputClass { get; init; }        // "form-control"
    public string TextAreaClass { get; init; }     // "form-control"
    public string SelectClass { get; init; }       // "form-select"
    public string CheckboxClass { get; init; }     // "form-check-input"
    public string LabelClass { get; init; }        // "form-label"
    public string CheckboxLabelClass { get; init; } // "form-check-label"
    public string ValidationClass { get; init; }   // "invalid-feedback"
}
```

Override globally through DI or per-call with `with {}`.

## Sort headers

```csharp
var header = Model.SortHeader("/items/list", BootstrapHeaderOptions.Default with
{
    SortButtonClass = "btn btn-link p-0",
    GlyphSpanClass = "sort-glyph"
});
```

`BootstrapHeaderOptions` properties:

| Property | Default |
|----------|---------|
| `SortButtonClass` | `"btn btn-link p-0 text-decoration-none"` |
| `GlyphSpanClass` | `"sort-glyph ms-1"` |

## Pager

```csharp
var ctx = Model.PagerContext("/items/list", BootstrapPagerOptions.Default with
{
    PagerActiveClass = "btn btn-primary btn-sm"
});
@ctx.Render()
```

`BootstrapPagerOptions` properties:

| Property | Default |
|----------|---------|
| `PagerContainerClass` | `"d-flex align-items-center gap-2 flex-wrap"` |
| `PagerButtonClass` | `"btn btn-outline-secondary btn-sm"` |
| `PagerActiveClass` | `"btn btn-secondary btn-sm"` |
| `PagerEllipsisClass` | `"px-1"` |
| `PageSizeSelectClass` | `"form-select form-select-sm w-auto"` |

## See also

- [Forms](forms.md)
- [Dialogs](dialogs.md)
- [Toasts](toasts.md)
- [List Features](list-features.md)
- [Lookup](lookup.md)
- [Configuration](configuration.md)
- [Page Shell](page-shell.md)
