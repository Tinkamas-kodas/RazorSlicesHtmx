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

@f.Code.Label("Code")
@f.Code.Input(placeholder: "ITEM-001", autoFocus: true)
@f.Code.Validation()

@f.Name.Label("Name")
@f.Name.Input()
@f.Name.Validation()

@f.Note.TextArea(placeholder: "Optional note", rows: 3)
@f.Note.Validation()

@f.IsActive.Checkbox()
@f.IsActive.Label("Active")

@f.Status.Select(statusOptions, emptyOption: "— Select —")
@f.Status.Validation()
```

### Helper reference

| Helper | Signature | Notes |
|--------|-----------|-------|
| `.Label(text)` | `Label(string text, BootstrapFieldOptions? options = null)` | Uses `CheckboxLabelClass` on `FieldContext<bool>` |
| `.Input(...)` | `Input(string? type, string? placeholder, bool autoFocus, bool required, bool disabled, bool readOnly, BootstrapFieldOptions?)` | Adds `is-invalid` when field has errors |
| `.TextArea(...)` | `TextArea(string? placeholder, int? rows, BootstrapFieldOptions?)` | |
| `.Checkbox(...)` | `Checkbox(bool disabled, BootstrapFieldOptions?)` | Auto-generates hidden false input + checkbox |
| `.Select(...)` | `Select(IEnumerable<SelectListItem> options, string? emptyOption, BootstrapFieldOptions?)` | |
| `.Validation()` | `Validation(BootstrapFieldOptions?)` | Renders `invalid-feedback` div; nothing when no errors |
| `.Hidden()` | `Hidden()` | Renders hidden input |

All helpers accept an optional `BootstrapFieldOptions` to override CSS classes per-call.

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
- [Configuration](configuration.md)
- [Page Shell](page-shell.md)
