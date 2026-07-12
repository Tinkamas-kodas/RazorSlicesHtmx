# Triggers

HTMX triggers let the server fire client-side custom events as part of a response.

## HX-Trigger vs HX-Trigger-After-Swap

| Header | Fires | Use for |
|--------|-------|---------|
| `HX-Trigger` | Before DOM swap | Events that don't depend on swapped HTML (`dialog:clear`, list refresh) |
| `HX-Trigger-After-Swap` | After all DOM swaps (primary + OOB) | Events that require swapped HTML to be in DOM (`show-dialog`, `show-toast`) |

## FeatureResultBuilder

```csharp
// HX-Trigger (before swap)
return Result.Page(detail)
    .WithTrigger("items:refresh")
    .Build();

// Both
return Result.Page(detail)
    .WithTrigger("items:refresh")   // fires before
    .ClearDialog()                  // adds "dialog:clear" to HX-Trigger
    .WithToast("Saved")             // adds "show-toast" to HX-Trigger-After-Swap
    .Build();
```

## HtmxFragmentResult

```csharp
// Simple event
HtmxFragmentResult.Create(primary)
    .WithTrigger("items:refresh")
    .Build();

// JSON payload event
HtmxFragmentResult.Create(primary)
    .WithTrigger("items:update", """{"id":42}""")
    .Build();

// After-swap
HtmxFragmentResult.Create(primary)
    .WithTriggerAfterSwap("show-dialog")
    .Build();

// After-swap with JSON payload
HtmxFragmentResult.Create(primary)
    .WithTriggerAfterSwap("show-toast", """{"title":"Saved","message":"Done","tone":"success"}""")
    .Build();
```

## Hx.* header constants

All `HX-` header names are available as constants in `Hx` and `Hx.Swap`:

```csharp
// Headers
Hx.Request            // "HX-Request"
Hx.Target             // "HX-Target"
Hx.CurrentUrl         // "HX-Current-URL"
Hx.Location           // "HX-Location"
Hx.PushUrl            // "HX-Push-Url"
Hx.ReplaceUrl         // "HX-Replace-Url"
Hx.Reswap             // "HX-Reswap"
Hx.Retarget           // "HX-Retarget"
Hx.Trigger            // "HX-Trigger"
Hx.TriggerAfterSwap   // "HX-Trigger-After-Swap"

// Swap strategies
Hx.Swap.InnerHtml     // "innerHTML"
Hx.Swap.OuterHtml     // "outerHTML"
Hx.Swap.BeforeBegin   // "beforebegin"
Hx.Swap.AfterBegin    // "afterbegin"
Hx.Swap.BeforeEnd     // "beforeend"
Hx.Swap.AfterEnd      // "afterend"
Hx.Swap.Delete        // "delete"
Hx.Swap.None          // "none"
```

## Listening in JavaScript

```javascript
document.body.addEventListener("items:refresh", () => {
    htmx.trigger("#items-list", "refresh");
});

document.body.addEventListener("show-toast", event => {
    const { title, message, tone } = event.detail ?? {};
    // create and show toast
});
```

## See also

- [Response Building](response-building.md)
- [Dialogs](dialogs.md)
- [Toasts](toasts.md)
