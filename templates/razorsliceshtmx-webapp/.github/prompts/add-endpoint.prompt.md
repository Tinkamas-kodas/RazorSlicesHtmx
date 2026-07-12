---
agent: agent
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
    return Result.Page(_{FeatureName}Page.Create(model))
        )
    .WithPageTitle("{FeatureName}")
        .Build();
});
```

**Toast-only response** (e.g., after delete):
```csharp
app.MapPost("{route}/delete", (int id) =>
{
    // TODO: delete the item
    return Result.Page(_Empty.Create())
        .WithTrigger("{FeatureName}.ListRefresh")
        .WithToast("Item deleted.")
        .Build();
});
```

**Dialog response** (show a Bootstrap modal):
```csharp
app.MapGet("{route}/confirm-delete/{id:int}", (int id) =>
{
    var model = _content.CreateDeleteDialogModel(id);
    return Result.ShowDialog(_{Entity}Delete.Create(model));
});
```

**With retarget** (swap a different element than the default):
```csharp
app.MapGet("{route}/details", (int id) =>
{
    var model = _content.GetDetails(id);
    return Result.Page(_DetailsSlice.Create(model))
        .WithRetarget("#details-panel")
        .WithReswap(HtmxSwap.InnerHtml)
        .Build();
});
```

## Important:
- All POST endpoints should accept antiforgery validation and use the shared HTMX token header adapter.
- For form posts, prefer generated `BindAsync(...)` / `FromForm(...)` request models over manual `[FromForm]` binding when the model is a partial upsert request.
- Use `Result.Page(detail)` — on HTMX requests the detail slice is the fragment; on full-page requests it is wrapped in the page shell
- For GET endpoints, set contextual title with `.WithPageTitle(...)` when needed (otherwise default is `NavigationRouteItem.Label`)
- Use `.WithTrigger()` to trigger client-side HTMX events (e.g., refresh a list)
- Use `.WithRetarget()` / `.WithReswap()` to override the default swap target
- Create a corresponding `.cshtml` slice for the response
