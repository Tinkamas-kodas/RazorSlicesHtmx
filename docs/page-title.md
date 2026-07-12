# Page Title

## Default behaviour

The page title is derived from `NavigationRouteItem.Label` automatically. No code required for the common case.

## Override per response

Use `WithPageTitle(...)` to override the title for a specific response:

```csharp
app.MapGet("/items/edit/{id:int}", (int id, AppDbContext db) =>
{
    var entity = db.Items.Find(id);
    return Result.Page(_EditPage.Create(_content.CreateEditModel(entity)))
        .WithPageTitle($"Edit — {entity.Name}")
        .Build();
});
```

## How it is applied

| Response type | How title is used |
|---------------|-------------------|
| Full-page | `FeatureShellContext.PageTitle` is passed to `IFeaturePageRenderer.RenderPage(shellContext)` |
| HTMX fragment | OOB `<title hx-swap-oob="true">...</title>` is appended so the browser tab title updates |

The app page renderer is responsible for rendering the title in the `<title>` tag on full-page loads. HTMX fragment responses update the browser tab automatically without any JS.

## See also

- [Response Building](response-building.md)
- [Page Shell](page-shell.md)
- [Feature Modules](feature-modules.md)
