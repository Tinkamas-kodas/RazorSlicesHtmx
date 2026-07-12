# Configuration

## RazorSlicesHtmxOptions

Shared options from `RazorSlicesHtmx.AspNetCore`. Bind from `appsettings.json` or configure in code.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DetailTargetSelector` | `string` | `"#details-pane"` | HTMX target for page/detail navigation |
| `DialogHostSelector` | `string` | `"#dialog-host"` | Selector for dialog OOB updates |
| `DialogClass` | `string?` | `null` | CSS class applied to the dialog wrapper |
| `ToastHostSelector` | `string` | `"#toast-host"` | Selector for toast container |
| `ToastDelayMilliseconds` | `int?` | `null` | Toast auto-hide delay in ms |
| `DefaultOobSwap` | `string` | `"innerHTML"` | Default HTMX OOB swap mode |
| `FieldOptions` | `object?` | `null` | Framework-specific field options (Bootstrap5 sets `BootstrapFieldOptions`) |

### appsettings.json

```json
{
  "RazorSlicesHtmx": {
    "DetailTargetSelector": "#details-pane",
    "DialogHostSelector": "#dialog-host",
    "DialogClass": "modal-dialog modal-dialog-centered modal-lg",
    "ToastHostSelector": "#toast-host",
    "ToastDelayMilliseconds": 3200,
    "DefaultOobSwap": "innerHTML"
  }
}
```

### Code binding

```csharp
builder.Services.Configure<RazorSlicesHtmxOptions>(
    builder.Configuration.GetSection("RazorSlicesHtmx"));

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
```

### Bootstrap5 fallback defaults

`AddRazorSlicesHtmxBootstrap5()` uses `PostConfigure` to apply Bootstrap-specific fallbacks. These only apply when the app has not already set the value:

- `DialogClass` → `modal-dialog modal-dialog-centered`
- `ToastDelayMilliseconds` → `2600`

---

## BootstrapFieldOptions

CSS class defaults for Bootstrap form field helpers. Set globally via DI:

```csharp
builder.Services.AddRazorSlicesHtmxBootstrap5(options =>
{
    options.FieldOptions = BootstrapFieldOptions.Default with
    {
        InputClass = "form-control form-control-sm",
        LabelClass = "form-label fw-semibold"
    };
});
```

| Property | Default |
|----------|---------|
| `InputClass` | `"form-control"` |
| `TextAreaClass` | `"form-control"` |
| `SelectClass` | `"form-select"` |
| `CheckboxClass` | `"form-check-input"` |
| `LabelClass` | `"form-label"` |
| `CheckboxLabelClass` | `"form-check-label"` |
| `ValidationClass` | `"invalid-feedback"` |

---

## BootstrapHeaderOptions

CSS defaults for sort header buttons:

| Property | Default |
|----------|---------|
| `SortButtonClass` | `"btn btn-link p-0 text-decoration-none"` |
| `GlyphSpanClass` | `"sort-glyph ms-1"` |

Override with `with {}` per-call or set `BootstrapHeaderOptions.Default` statically.

---

## BootstrapPagerOptions

CSS defaults for the pager:

| Property | Default |
|----------|---------|
| `PagerContainerClass` | `"d-flex align-items-center gap-2 flex-wrap"` |
| `PagerButtonClass` | `"btn btn-outline-secondary btn-sm"` |
| `PagerActiveClass` | `"btn btn-secondary btn-sm"` |
| `PagerEllipsisClass` | `"px-1"` |
| `PageSizeSelectClass` | `"form-select form-select-sm w-auto"` |

---

## See also

- [Getting Started](getting-started.md)
- [Bootstrap5](bootstrap5.md)
- [Dialogs](dialogs.md)
- [Toasts](toasts.md)
