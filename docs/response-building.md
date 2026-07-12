# Response Building

Two builders handle HTMX response composition.

| Builder | When to use |
|---------|------------|
| `FeatureResultBuilder` | Feature endpoints — auto-detects full-page vs fragment based on `HX-Request` header. Implements `IResult` directly. |
| `HtmxFragmentResult` | Fragment-only endpoints — always returns a fragment. Implements `IResult` directly. |

---

## FeatureResultBuilder

Access via `Result` in `BaseFeatureModule`, or inject `FeatureResultBuilder` directly.

```csharp
return Result.Page(_FeaturePage.Create(model))
    .WithState(search)
    .WithToast("Saved");
```

### Full API

| Method | Description |
|--------|-------------|
| `WithNav(bool)` | Override whether navigation OOB is included (default: `INavOobStrategy` decides) |
| `WithOob(slice)` | Append a pre-configured OOB slice |
| `WithOob(IEnumerable<slice>)` | Append multiple OOB slices |
| `WithOob(selector, content, swap?)` | Append a targeted OOB update |
| `WithDialog(content)` | Render dialog into `DialogHostSelector` + fire `show-dialog` after swap |
| `ClearDialog()` | Fire `dialog:clear` trigger to clear the dialog host |
| `WithToast(message, title?, tone?)` | Queue a toast notification (default `tone`: `Success`) |
| `WithTrigger(eventName)` | Add `HX-Trigger` event (fires before DOM swap) |
| `WithState(model)` | Add OOB state update + register in `HtmxStateContext` |
| `WithWorkspace<TFactory>()` | Wrap detail in workspace; auto-selects inner vs full based on `HX-Target` |
| `WithNavigation(url, replace?)` | Add `HX-Replace-Url` or `HX-Push-Url`; suppresses nav OOB by default |
| `WithLocation(url, replace?)` | Add `HX-Location` for client-side navigation with optional toast |
| `WithRetarget(selector)` | Add `HX-Retarget` |
| `WithReswap(swapMode)` | Add `HX-Reswap` |
| `WithPageTitle(title)` | Override page title for this response |
| `Build()` | Explicitly build the result (optional — `Builder` implements `IResult` directly) |

`Result.ShowDialog(content)` returns an `IResult` directly — no chain needed:

```csharp
return Result.ShowDialog(_DeleteConfirm.Create(model));
```

---

## HtmxFragmentResult

For endpoints that always return a fragment — no full-page rendering, no auth filtering.

```csharp
return HtmxFragmentResult.Create(primary)
    .WithOob("#sidebar", sidebar)
    .WithTriggerAfterSwap("show-dialog");
```

### Full API

| Method | Description |
|--------|-------------|
| `Create(primary)` | Static factory — starts a builder with the primary slice |
| `WithOob(selector, content, swap?)` | Targeted OOB update (default swap: `Hx.Swap.InnerHtml`) |
| `WithOob(params slice[])` | Direct OOB slices (pre-configured `hx-swap-oob`) |
| `WithOob(IEnumerable<slice>)` | Multiple direct OOB slices |
| `WithRawOob(IHtmlContent)` | Raw HTML content as OOB |
| `WithRawOob(IEnumerable<IHtmlContent>)` | Multiple raw OOB parts |
| `WithTrigger(eventName)` | Add `HX-Trigger` event |
| `WithTrigger(eventName, jsonValue)` | Add `HX-Trigger` with JSON payload |
| `WithTriggerAfterSwap(eventName)` | Add `HX-Trigger-After-Swap` event |
| `WithTriggerAfterSwap(eventName, jsonValue)` | Add `HX-Trigger-After-Swap` with JSON payload |
| `WithRetarget(selector)` | Add `HX-Retarget` |
| `WithReswap(swapMode)` | Add `HX-Reswap` |
| `Build()` | Explicitly build the result (optional — `Builder` implements `IResult` directly) |

---

## Non-HTMX behaviour

`FeatureResultBuilder.Builder` (returned by `Result.Page(...)`) implements `IResult` directly — `.Build()` is optional:

- **HTMX request** → returns fragment + OOB parts
- **Non-HTMX request** → calls `IFeaturePageRenderer.RenderPage(shellContext)` for full page
- **Non-HTMX + `WithLocation`** → `Results.Redirect(url)`
- **Non-HTMX + `WithNavigation`** → `Results.Redirect(url)`

---

## See also

- [Dialogs](dialogs.md)
- [Toasts](toasts.md)
- [Triggers](triggers.md)
- [Retarget & Reswap](retarget-reswap.md)
- [State](state.md)
- [Navigation & URL](navigation-url.md)
- [Page Title](page-title.md)
