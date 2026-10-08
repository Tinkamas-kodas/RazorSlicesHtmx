# Triggers

HTMX triggers let the server fire client-side custom events as part of a response.

## HX-Trigger

In htmx 4, `HX-Trigger` fires **after the swap completes**.

When `HX-Trigger` is represented as JSON (for example when combining a simple trigger and a JSON trigger payload), each event should have a non-null payload object.
`RazorSlicesHtmx` serializes simple triggers as `{}` in that mode so htmx 4 event dispatch remains stable.

| Header | Fires | Notes |
|--------|-------|-------|
| `HX-Trigger` | After all DOM swaps (primary + OOB) | Use for all events, including those that require swapped HTML |

### HTMX 4 payload note

Preferred mixed trigger header shape:

```json
{
    "Items.ListRefresh": {},
    "show-toast": { "title": "Saved", "message": "Item created", "tone": "success" }
}
```

Avoid `null` payloads for simple events in mixed JSON trigger headers.

## FeatureResultBuilder

```csharp
// Fire event after swap
return Result.Page(detail)
    .WithTrigger("items:refresh")
    .Build();

// Multiple triggers
return Result.Page(detail)
    .WithTrigger("items:refresh")
    .ClearDialog()                  // adds "dialog:clear" to HX-Trigger
    .WithToast("Saved")             // adds "show-toast" to HX-Trigger
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
```

## Hx.* header constants

All `HX-` header names are available as constants in `Hx` and `Hx.Swap`:

```csharp
// Headers
Hx.Request            // "HX-Request"
Hx.RequestType        // "HX-Request-Type" (htmx 4: "partial" or "full")
Hx.Target             // "HX-Target"
Hx.CurrentUrl         // "HX-Current-URL"
Hx.Location           // "HX-Location"
Hx.PushUrl            // "HX-Push-Url"
Hx.ReplaceUrl         // "HX-Replace-Url"
Hx.Reswap             // "HX-Reswap"
Hx.Retarget           // "HX-Retarget"
Hx.Trigger            // "HX-Trigger" (fires after swap in htmx 4)

// Swap strategies
Hx.Swap.InnerHtml     // "innerHTML"
Hx.Swap.OuterHtml     // "outerHTML"
Hx.Swap.InnerMorph    // "innerMorph" (htmx 4: morphs children, preserving DOM)
Hx.Swap.BeforeBegin   // "beforebegin"
Hx.Swap.AfterBegin    // "afterbegin"
Hx.Swap.BeforeEnd     // "beforeend"
Hx.Swap.AfterEnd      // "afterend"
Hx.Swap.Delete        // "delete"
Hx.Swap.None          // "none"
```

## Listening in JavaScript

```javascript
document.addEventListener("items:refresh", () => {
    htmx.trigger("#items-list", "refresh");
});

document.addEventListener("show-toast", event => {
    const { title, message, tone } = event.detail ?? {};
    // create and show toast
});
```

## See also

- [Response Building](response-building.md)
- [Dialogs](dialogs.md)
- [Toasts](toasts.md)
