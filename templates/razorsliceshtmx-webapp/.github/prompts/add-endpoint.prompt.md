---
mode: agent
description: "Add a new HTMX endpoint to an existing feature"
---

# Add an Endpoint to a Feature

Add a new HTMX endpoint (GET or POST) to an existing feature module.

## Ask the user for:
1. **Feature name** — which feature to add the endpoint to
2. **HTTP method** — GET or POST
3. **Route** — the endpoint path (e.g., "/products/delete")
4. **Purpose** — what the endpoint does (e.g., "delete an item", "toggle status", "search")
5. **Response type** — full page, fragment, toast only, or dialog

## Implementation Pattern

### In `{FeatureName}Endpoints.cs`, inside `MapEndpoints()`:

**Fragment response** (swaps part of the page):
```csharp
app.MapGet("{route}/{action}", (int id) =>
{
    var model = _content.GetModel(id);
    return Result.For(_{FeatureName}Page.Create(model))
        .AsFragment(_FragmentSlice.Create(model))
        .BuildAsync();
});
```

**Toast-only response** (e.g., after delete):
```csharp
app.MapPost("{route}/delete", (int id) =>
{
    // TODO: delete the item
    return Result.For(_Empty.Create())
        .WithTrigger("{FeatureName}.ListRefresh")
        .WithToast("Item deleted.")
        .BuildAsync();
}).DisableAntiforgery();
```

**Dialog response** (show a modal):
```csharp
app.MapGet("{route}/confirm-delete", (int id) =>
    Result.For(_DeleteDialog.Create(new DeleteModel(id, "Item Name")))
        .BuildAsync());
```

**With retarget** (swap a different element than the default):
```csharp
app.MapGet("{route}/details", (int id) =>
{
    var model = _content.GetDetails(id);
    return Result.For(_DetailsSlice.Create(model))
        .WithRetarget("#details-panel")
        .WithReswap(HtmxSwap.InnerHtml)
        .BuildAsync();
});
```

## Important:
- All POST endpoints need `.DisableAntiforgery()`
- Use `.AsFragment()` when the endpoint should return just a fragment for HTMX but the full page for direct browser navigation
- Use `.WithTrigger()` to trigger client-side HTMX events (e.g., refresh a list)
- Use `.WithRetarget()` / `.WithReswap()` to override the default swap target
- Create a corresponding `.cshtml` slice for the response
