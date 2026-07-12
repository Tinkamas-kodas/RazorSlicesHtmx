# Retarget & Reswap

Both `FeatureResultBuilder` and `HtmxFragmentResult` support `HX-Retarget` and `HX-Reswap`. These let the server override where and how the response is swapped into the DOM — regardless of what the original HTMX request specified.

## WithRetarget

Overrides the target element. The response goes to a different element than the one the request was aimed at:

```csharp
return Result.Page(detail)
    .WithRetarget("#edit-form")
    .Build();
```

## WithReswap

Overrides the swap strategy. Use constants from `Hx.Swap`:

```csharp
return Result.Page(detail)
    .WithReswap(Hx.Swap.BeforeEnd)  // append to container
    .Build();
```

## Combined — validation error pattern

The most common use case: redirect a validation failure into the form element with full replacement:

```csharp
// Validation failed — replace form element with error version
return Result.Page(detail)
    .WithRetarget("#edit-form")
    .WithReswap(Hx.Swap.OuterHtml)
    .Build();
```

With `HtmxFragmentResult` (fragment-only endpoint):

```csharp
return HtmxFragmentResult.Create(formSliceWithErrors)
    .WithRetarget("#edit-form")
    .WithReswap(Hx.Swap.OuterHtml)
    .Build();
```

## Hx.Swap values

| Constant | Value | Description |
|----------|-------|-------------|
| `Hx.Swap.InnerHtml` | `innerHTML` | Replace inner content (default) |
| `Hx.Swap.OuterHtml` | `outerHTML` | Replace element entirely |
| `Hx.Swap.BeforeBegin` | `beforebegin` | Insert before element |
| `Hx.Swap.AfterBegin` | `afterbegin` | Insert at start of element |
| `Hx.Swap.BeforeEnd` | `beforeend` | Append inside element |
| `Hx.Swap.AfterEnd` | `afterend` | Insert after element |
| `Hx.Swap.Delete` | `delete` | Remove element |
| `Hx.Swap.None` | `none` | No swap |

## See also

- [Response Building](response-building.md)
- [Triggers](triggers.md)
- [Validation](validation.md)
