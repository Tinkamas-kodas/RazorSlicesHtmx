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
The **host list slice** contains the toolbar (search form) and embeds `_ItemsTable` as a partial.
The toolbar does NOT get refreshed when the table refreshes — only `_ItemsTable` swaps on `ListRefresh`:
```razor
@inherits RazorSlice<{FeatureName}ListModel>

<section class="section-panel">
    @* Toolbar — stays stable, never flickers on list refresh *@
    <form hx-get="/{route}/list"
          hx-include="#@{FeatureName}ListQuery.StateId"
          hx-vals='{"Page":"1"}'
          hx-target="#@{FeatureName}ListQuery.StateId"
          hx-swap="@HtmxSwap.OuterHtml"
          hx-push-url="true">

        <input type="text" name="Search" value="@Model.Search?.Search" placeholder="Search...">
        <button type="submit">Search</button>
    </form>

    @* Table container — this is the refresh target *@
    <div id="{featurename}-table"
         hx-get="/{route}/list"
         hx-trigger="{FeatureName}.ListRefresh from:body"
         hx-include="#@{SearchModel}.StateId, #@{FeatureName}ListQuery.StateId"
         hx-target="this"
         hx-swap="@HtmxSwap.InnerHtml"
         hx-push-url="true">
        @await RenderPartialAsync(_{FeatureName}Table.Create(Model))
    </div>
</section>
```

### `Features/{FeatureName}/Slices/_{FeatureName}Table.cshtml`
The **table-only slice** — sort headers, rows, and pager. This is the fragment returned by the list endpoint:
```razor
@inherits RazorSlice<{FeatureName}ListModel>

@{
    var header = Model.SortHeader("/{route}/list", BootstrapHeaderOptions.Default);
}

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

<form class="list-request-form"
      hx-get="/{route}/list"
      hx-include="#@{SearchModel}.StateId, #@{FeatureName}ListQuery.StateId"
      hx-target="#{featurename}-table"
      hx-swap="@HtmxSwap.InnerHtml"
      hx-push-url="true">

    <table class="table">
        <thead>
            <tr>
                @* SortHeader renders a clickable button with sort direction indicator *@
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

    @await RenderPartialAsync(_Pager.Create(Model.ToPagerModel("/{route}/list")))
</form>
```

### Slice separation rationale

```
_FeaturePage.cshtml      ← host: state serialization + feature-details wrapper
  └─ _ListPage.cshtml    ← toolbar (search form) + table container div
       └─ _{Name}Table.cshtml  ← table + headers + pager (refresh target)
```

- **Search/toolbar** stays outside the refresh target → no flicker when filtering
- **Table div** (`#{featurename}-table`) is the `hx-target` for sort, page, and refresh
- **ListRefresh trigger** targets only the table div, not the whole list page
- The list endpoint returns `_{Name}Table` as the `.AsFragment()` slice

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
app.MapGet("{route}/list", ([AsParameters] {SearchModel} search, {FeatureName}ListQuery query) =>
{
    var model = _content.CreateListModel(search, query);
    return Result.For(_ListPage.Create(model))
        .AsFragment(_{FeatureName}Table.Create(model))
        .WithState(search)
        .WithState(query)
        .BuildAsync();
});
```

Note: `.AsFragment(_{FeatureName}Table)` — the fragment is the **table-only** slice, not the full list page. This ensures only the table swaps on HTMX requests while the toolbar stays stable.

### Update `_{FeatureName}Page.cshtml` (host)
- Render state hidden divs: `@Model.Search.Serialize()` and `@Model.Request.Serialize()`
- Wrap content in a div with `hx-trigger="{FeatureName}.ListRefresh from:body"` targeting the table container

## Important:
- **Toolbar stays outside the refresh target** — search input never flickers on list refresh
- **Table div** is the swap target for sort/page/refresh — only the table re-renders
- `.AsFragment()` returns `_{FeatureName}Table` (table-only), not `_ListPage` (toolbar+table)
- `ListRequest<T>` is `partial` — generator auto-creates `SortMap` from row model properties
- `hx-include="#@{SearchModel}.StateId, #@{Query}.StateId"` preserves all state across requests
- `.WithState(search).WithState(query)` sends OOB state updates on fragment responses
