# Dialogs

## Opening a dialog from a feature endpoint

Use `Result.ShowDialog(content)` to open a dialog — returns the dialog slice as the response:

```csharp
return Result.ShowDialog(_DeleteConfirm.Create(model));
```

Use `WithDialog(content)` when you need to **both** navigate to a page **and** open a dialog in the same response:

```csharp
return Result.Page(detail)
    .WithDialog(_SomeNotification.Create(model));
```

## Closing a dialog programmatically

Use `ClearDialog()` to fire `dialog:clear`, which empties the dialog host and clears Bootstrap modal artifacts (backdrop, `modal-open` class):

```csharp
return Result.Page(detail)
    .WithTrigger(Consts.RefreshListTrigger)
    .WithToast("Item deleted", tone: ToastTone.Warning)
    .ClearDialog();
```

## Keeping the dialog open on validation failure

Render the dialog slice as the primary fragment and use `WithRetarget`/`WithReswap` to replace the dialog content in-place:

```csharp
// Validation failed — re-render form inside the dialog, keep modal open
return Result.Page(_DeleteConfirm.Create(request));
```

The dialog form should target its own container with `outerHTML` swap so the modal stays open:

```cshtml
<form hx-post="/items/delete"
      hx-target="#delete-dialog"
      hx-swap="outerHTML">
  <div id="delete-dialog" class="modal-content">
    ...
  </div>
</form>
```

## HtmxFragmentResult — dialog without feature builder

```csharp
return HtmxFragmentResult.Create(resultBuilder.CreateDialog(content))
    .WithTriggerAfterSwap("show-dialog")
    .Build();
```

## Trigger timing

| Trigger | Fires |
|---------|-------|
| `show-dialog` | `HX-Trigger-After-Swap` — after OOB dialog HTML is in the DOM |
| `dialog:clear` | `HX-Trigger` — before DOM swap (just clears the host) |

Using `HX-Trigger-After-Swap` for `show-dialog` ensures the Bootstrap modal finds its HTML when `.show()` is called.

## Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `DialogHostSelector` | `#dialog-host` | CSS selector for the dialog OOB target |
| `DialogClass` | `modal-dialog modal-dialog-centered` | CSS class applied to the dialog wrapper |

See [Configuration](configuration.md) for details.

## See also

- [Response Building](response-building.md)
- [Toasts](toasts.md)
- [Configuration](configuration.md)
- [Bootstrap5](bootstrap5.md)
