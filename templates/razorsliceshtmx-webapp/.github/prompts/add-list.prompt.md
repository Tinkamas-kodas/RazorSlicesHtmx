---
mode: agent
description: "Add a list page with a data table to an existing feature"
---

# Add a List Page to a Feature

Add a list page with a data table and HTMX refresh support to an existing feature module.

## Ask the user for:
1. **Feature name** — which existing feature to add the list to
2. **Entity name** — the item shown in rows (e.g., "Product")
3. **Columns** — what properties to display in the table (e.g., "Name, Price, IsEnabled")

## Create these files:

### `Features/{FeatureName}/Models/{Entity}RowModel.cs`
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {Entity}RowModel(
    int Id,
    {columns as properties});
```

### `Features/{FeatureName}/Models/{FeatureName}ListModel.cs`
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {FeatureName}ListModel(
    IReadOnlyList<{Entity}RowModel> Items);
```

### `Features/{FeatureName}/Slices/_ListPage.cshtml`
```razor
@inherits RazorSlice<{FeatureName}ListModel>

<div id="{featurename}-list"
     hx-get="{route}/list"
     hx-trigger="{FeatureName}.ListRefresh from:body"
     hx-swap="@HtmxSwap.OuterHtml">

    <table class="table">
        <thead>
            <tr>
                {th elements for each column}
            </tr>
        </thead>
        <tbody>
            @foreach (var item in Model.Items)
            {
                <tr>
                    {td elements for each column}
                </tr>
            }
        </tbody>
    </table>
</div>
```

## Update existing files:

### Update `{FeatureName}ContentService.cs`
- Add `CreateListModel()` method that returns sample data
- Update `PageDefinition` to pass list model to the page slice

### Update `{FeatureName}Endpoints.cs`
Add a list endpoint:
```csharp
app.MapGet("{route}/list", () =>
{
    var model = _content.CreateListModel();
    return Result.For(_{FeatureName}Page.Create(model))
        .AsFragment(_ListPage.Create(model))
        .BuildAsync();
});
```

### Update `_{FeatureName}Page.cshtml`
- Change to `@inherits RazorSlice<{FeatureName}ListModel>`
- Include `@(await RenderPartialAsync(_ListPage.Create(Model)))` in the page body

## Important:
- The list fragment uses `hx-trigger="{FeatureName}.ListRefresh from:body"` so it auto-refreshes when triggered
- Use `.AsFragment()` so HTMX requests get only the list, not the full page
