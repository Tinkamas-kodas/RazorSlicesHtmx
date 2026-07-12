# Forms

## GenerateHtmlNames

`[GenerateHtmlNames]` generates compile-time HTML field metadata for a model class. No runtime expression parsing.

```csharp
[GenerateHtmlNames]
public sealed class ItemUpsertRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}
```

### Static accessors — metadata only

```cshtml
@using For = MyApp.HtmlNames.MyApp.Features.Items.Models.ItemUpsertRequestHtml

<label for="@For.Code.Id">Code</label>
<input id="@For.Code.Id" name="@For.Code.Name">
```

Each `For.<Field>` exposes: `.Name`, `.Id`, `.Path` — implements `IPropertyAccessor`.

### Instance context via `.For()` — metadata + values

```cshtml
@using MyApp.HtmlNames.MyApp.Features.Items.Models

@{ var f = Model.Request.For(); }

<input id="@f.Code.Id" name="@f.Code.Name" value="@f.Code.Value">
<input id="@f.Name.Id" name="@f.Name.Name" value="@f.Name.Value">
```

### With validation errors

Pass `IHaveValidationResult.Errors` to `.For()`:

```cshtml
@{ var f = Model.Request.For(Model.Errors); }
@* f.Code.HasErrors, f.Code.FieldErrors available *@
```

### Bootstrap 5 form helpers

From `RazorSlicesHtmx.Bootstrap5`:

```cshtml
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.Request.For(Model.Errors); }

@f.Code.Label("Code")
@f.Code.Input(placeholder: "ITEM-001", autoFocus: true)
@f.Code.Validation()

@f.Name.Label("Name")
@f.Name.Input()
@f.Name.Validation()

@f.IsEnabled.Checkbox()
@f.IsEnabled.Label("Enabled")
```

Available helpers: `.Label(text)`, `.Input(type?, placeholder?, autoFocus?, required?, disabled?, readOnly?)`, `.TextArea(placeholder?, rows?)`, `.Checkbox(disabled?)`, `.Select(options, emptyOption?)`, `.Validation()`.

All helpers accept an optional `BootstrapFieldOptions` to override CSS classes per-call. Global defaults configured via `BootstrapFieldOptions.Current` (set through DI — see [Bootstrap5](bootstrap5.md)).

### FieldContext&lt;T&gt;

Combines `IPropertyAccessor` with the property value. Useful for generic form components:

```csharp
static IHtmlContent RenderField<T>(FieldContext<T> field, string label)
{
    // field.Name, field.Path, field.Id, field.Value
}
```

## GenerateFormBinder

`[GenerateFormBinder]` generates `FromForm(IFormCollection)` and `BindAsync(HttpContext, ParameterInfo)` for a form model, enabling it to be used as a direct Minimal API parameter.

```csharp
[GenerateHtmlNames]
[GenerateFormBinder]
public sealed partial class ItemUpsertRequest : IHaveValidationResult
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } = [];
}
```

Endpoint:

```csharp
app.MapPost("/items/create", async (ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator) =>
{
    var result = await validator.ValidateAsync(request);
    if (!result.IsValid)
    {
        request.Errors = result.ToErrorDictionary();
        return Result.Page(_CreatePage.Create(request));
    }
    // ...
});
```

## IHaveValidationResult

Contract for models that carry validation errors. Implemented manually on form request models:

```csharp
public sealed partial class ItemUpsertRequest : IHaveValidationResult
{
    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } = [];
}
```

The error dictionary key is the field name (case-insensitive). Used by `FieldContext<T>.HasErrors` and `.FieldErrors`.

## See also

- [Validation](validation.md)
- [Bootstrap5](bootstrap5.md)
- [State](state.md)
- [List Features](list-features.md)
