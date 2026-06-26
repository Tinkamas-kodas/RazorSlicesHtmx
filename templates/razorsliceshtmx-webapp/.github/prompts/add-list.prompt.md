---
agent: agent
description: "Add a list page with a data table to an existing feature"
---

# Add a List Page to a Feature

Add a list page with a data table, sortable column headers, pager, and HTMX refresh support.

## Ask the user for:
1. **Feature name** — which existing feature to add the list to
2. **Entity name** — the item shown in rows (e.g., "Product")
3. **Columns** — what properties to display in the table (e.g., "Name, Price, IsEnabled")
4. **Default sort column** — which column is sorted by default
5. **Page size** — items per page (default: 10)

## Create these files:

### `Features/{FeatureName}/Models/{Entity}RowModel.cs`
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {Entity}RowModel(
    int Id,
    {columns as properties});
```

### `Features/{FeatureName}/Models/{FeatureName}ListQuery.cs`
Extends `ListRequest<T>` for sort/page state:
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed partial class {FeatureName}ListQuery : ListRequest<{Entity}RowModel>
{
    private const string DefaultSortBy = "{defaultSortColumn}";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = {pageSize};

    public {FeatureName}ListQuery()
    {
        SortBy = DefaultSortBy;
        Page = DefaultPage;
        PageSize = DefaultPageSize;
    }
}
```

### `Features/{FeatureName}/Models/{FeatureName}ListModel.cs`
Extends `ListResponse<T, TRequest>` which provides `Total`, `Request`, `Items`, `FromItem`, `ToItem`, `TotalPages`:
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {FeatureName}ListModel(
    int Total,
    {FeatureName}ListQuery Request,
    List<{Entity}RowModel> Items)
    : ListResponse<{Entity}RowModel, {FeatureName}ListQuery>(Total, Request, Items);
```

### `Features/{FeatureName}/Slices/_ListPage.cshtml`
Uses `SortHeader` for clickable column headers and `_Pager` for pagination:
```razor
@inherits RazorSlice<{FeatureName}ListModel>

@{
    var header = Model.SortHeader("/{route}/list", BootstrapHeaderOptions.Default);
}

<section class="section-panel">
    <p>
        @if (Model.Total == 0)
        {
            <text>No items found.</text>
        }
        else
        {
            <text>Showing @Model.FromItem–@Model.ToItem of @Model.Total items.</text>
        }
    </p>

    <form hx-get="/{route}/list"
          hx-include="#@{FeatureName}ListQuery.StateId"
          hx-target="#{featurename}-list"
          hx-swap="@HtmxSwap.OuterHtml">

        <div id="{featurename}-list"
             hx-get="/{route}/list"
             hx-trigger="{FeatureName}.ListRefresh from:body"
             hx-swap="@HtmxSwap.OuterHtml">

            <table class="table">
                <thead>
                    <tr>
                        @* SortHeader renders a clickable <a> with sort direction indicator *@
                        <th>@header.Render("{columnFieldName}", "{Column Label}")</th>
                        @* repeat for each column *@
                        <th class="text-end">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var item in Model.Items)
                    {
                        <tr>
                            <td>@item.{Column}</td>
                            @* repeat for each column *@
                        </tr>
                    }
                </tbody>
            </table>
        </div>

        <div class="mt-3">
            @await RenderPartialAsync(_Pager.Create(Model.ToPagerModel("/{route}/list")))
        </div>
    </form>
</section>
```

## Key concepts:

### Sort headers (`SortHeader`)
- `Model.SortHeader(getUrl, BootstrapHeaderOptions.Default)` creates a sort context
- `header.Render("fieldName", "Label")` renders a clickable `<a>` with sort direction indicator
- The field name must match a key in `ListQuery.SortMap` (or the property name)
- Clicking toggles between Asc/Desc and issues an HTMX GET with `SortBy` and `SortDirection` query params

### Pager (`_Pager` slice)
- `ListResponse<T, TRequest>` provides: `Total`, `TotalPages`, `FromItem`, `ToItem`
- `Model.ToPagerModel("/route")` creates a `PagerModel` for the built-in `_Pager` slice
- `_Pager` renders Bootstrap pagination with Previous/Next and page number buttons
- Alternative: `Model.PagerContext("/route", BootstrapPagerOptions.Default).Render()` for programmatic rendering

### `ListRequest<T>` base class
- Provides `Page`, `PageSize`, `SortBy`, `SortDirection` properties
- Has a `SortMap` dictionary mapping column names to sort expressions
- Use `.ToPagedList(request)` or `.ToPagedListAsync(source, request)` to paginate `IQueryable<T>`

## Update existing files:

### Update `{FeatureName}ContentService.cs`
- Add method returning sample data as `{FeatureName}ListModel`

### Update `{FeatureName}Endpoints.cs`
```csharp
app.MapGet("{route}/list", ({FeatureName}ListQuery query) =>
{
    var model = _content.CreateListModel(query);
    return Result.For(_{FeatureName}Page.Create(model))
        .AsFragment(_ListPage.Create(model))
        .BuildAsync();
});
```

## Important:
- The list fragment uses `hx-trigger="{FeatureName}.ListRefresh from:body"` for auto-refresh
- Use `.AsFragment()` so HTMX requests get only the list, not the full page
- `ListRequest<T>` is `partial` — add `SortMap` override to define sortable columns
- `hx-include="#@{Query}.StateId"` preserves sort/page state across requests
