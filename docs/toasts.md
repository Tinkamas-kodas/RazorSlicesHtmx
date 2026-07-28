# Toasts

## Showing a toast from an endpoint

```csharp
return Result.Page(detail)
    .WithToast("Item saved")                                           // default: title "Saved", tone Success
    .WithToast("Item deleted", title: "Deleted", tone: ToastTone.Warning)
    .Build();
```

Multiple toasts are allowed — each becomes a separate `show-toast` trigger event.

## ToastTone

`ToastTone` is a semantic enum. UI framework packages map it to CSS classes.

| Value | Bootstrap5 CSS |
|-------|---------------|
| `Info` | `text-bg-info` |
| `Success` | `text-bg-success` |
| `Warning` | `text-bg-warning` |
| `Error` | `text-bg-danger` |

The error middleware always uses `ToastTone.Error`. See [Error Handling](error-handling.md).

## How it works

`WithToast(...)` queues a `ToastModel`. On `Build()`, each toast is serialized as a `show-toast` JSON trigger on `HX-Trigger`:

```
HX-Trigger: {"show-toast":{"title":"Saved","message":"Item saved","tone":"success"}}
```

The app shell JavaScript listens for `show-toast` and creates the Bootstrap toast element dynamically.

## Toasts with HX-Location

When combined with `WithLocation(...)`, toasts fire after the location follow-up navigation completes — not before it:

```csharp
return Result.Page(detail)
    .WithLocation("/items")
    .WithToast("Item deleted", tone: ToastTone.Warning)
    .Build();
```

## WithToast signature

```csharp
Builder WithToast(string message, string title = "Saved", ToastTone tone = ToastTone.Success)
```

## Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `ToastHostSelector` | `#toast-host` | CSS selector for the toast container |
| `ToastDelayMilliseconds` | `2600` (Bootstrap5 default) | Auto-hide delay |

See [Configuration](configuration.md) for details.

## See also

- [Response Building](response-building.md)
- [Error Handling](error-handling.md)
- [Triggers](triggers.md)
- [Bootstrap5](bootstrap5.md)
