# Validation

## FluentValidation integration

`RazorSlicesHtmx.FluentValidation` maps a `ValidationResult` into the error dictionary used by the view layer.

```csharp
using FluentValidation.Results;
using RazorSlicesHtmx.FluentValidation.Extensions;

ValidationResult result = await validator.ValidateAsync(request);

// Returns IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]>
var errors = result.ToErrorDictionary();
```

The dictionary key is the property name (case-insensitive). Each entry is an array of `(errorCode, errorMessage)` tuples — one per failed rule on that field.

## Endpoint pattern

```csharp
app.MapPost("/items/create", async (ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator) =>
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        request.Errors = validationResult.ToErrorDictionary();
        return Result.Page(_CreatePage.Create(request));
    }

    // ... save and redirect
    return Result.Page(_CreatePage.Create(request))
        .WithLocation("/items")
        .WithToast("Item created")
        .Build();
});
```

## IHaveValidationResult

Models that carry validation errors implement this interface:

```csharp
public sealed partial class ItemUpsertRequest : IHaveValidationResult
{
    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } = [];
}
```

## Bootstrap5 validation helpers

From `RazorSlicesHtmx.Bootstrap5`:

```cshtml
@{ var f = Model.Request.For(Model.Errors); }

@f.Code.Input()       @* adds is-invalid CSS class when field has errors *@
@f.Code.Validation()  @* renders invalid-feedback div with error messages *@
```

`.Validation()` renders a `<div class="invalid-feedback">` with all error messages for that field. It renders nothing when the field has no errors.

See [Forms](forms.md) for full Bootstrap field helper reference.

## See also

- [Forms](forms.md)
- [Bootstrap5](bootstrap5.md)
- [Retarget & Reswap](retarget-reswap.md)
