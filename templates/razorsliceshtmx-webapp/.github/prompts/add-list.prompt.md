---
agent: agent
description: "Add a list page with a data table to an existing feature"
---

# Add a List Page to a Feature

Add a list page with a data table, sortable column headers, pager, search toolbar, and HTMX refresh support.

## Ask the user for:
1. **Feature name** — which existing feature to add the list to
2. **Entity name** — the item shown in rows (e.g., "Product")
3. **Columns** — what properties to display in the table (e.g., "Name, Price, IsEnabled")
4. **Default sort column** — which column is sorted by default
5. **Page size** — items per page (default: 5)

## Create these files:

### `Features/{FeatureName}/Models/{Entity}RowModel.cs`
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {Entity}RowModel(
    [property: SortDisable] int Id,
    {columns as properties});
```

Use `[SortDisable]` on Id so the generator skips it in the auto-generated SortMap.

### `Features/{FeatureName}/Models/{FeatureName}SearchModel.cs`
Search/filter state (separate from paging/sort state):
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

/// <summary>Search state for the list. Paging and sort lives in <see cref="{FeatureName}ListQuery"/>.</summary>
public sealed partial record {FeatureName}SearchModel(string? Search);
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
Extends `ListResponse<T, TRequest>` and carries the search model:
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {FeatureName}ListModel(
    int Total,
    {FeatureName}SearchModel Search,
    {FeatureName}ListQuery Request,
    List<{Entity}RowModel> Items)
    : ListResponse<{Entity}RowModel, {FeatureName}ListQuery>(Total, Request, Items);
```

### `Features/{FeatureName}/Slices/_FeaturePage.cshtml`
The **host slice** — serializes state into hidden divs, wraps `#feature-details` with the `ListRefresh` trigger:
```razor
@inherits RazorSlice<{FeatureName}ListModel>

<div id="{featurename}-workspace" class="section-panel">
    @Model.Search.Serialize()
    @Model.Request.Serialize()

    <div id="feature-details"
         hx-get="/{route}/list"
         hx-trigger="{FeatureName}.ListRefresh from:body"
         hx-include="#@{FeatureName}SearchModel.StateId, #@{FeatureName}ListQuery.StateId"
         hx-push-url="true"
         hx-target="#feature-details">
        @await RenderPartialAsync(_ListPage.Create(Model))
    </div>
</div>
```

### `Features/{FeatureName}/Slices/_ListPage.cshtml`
Toolbar (search form + "Create new" button) and renders the items list as a partial:
```razor
@inherits RazorSlice<{FeatureName}ListModel>

<article class="section-panel">
    <h2>{FeatureName}</h2>

    <div class="card border-0 shadow-sm mb-3">
        <div class="card-body p-4">
            <div class="d-flex justify-content-between align-items-end flex-wrap gap-2">
                <form class="d-flex align-items-end gap-2"
                      hx-get="/{route}/list"
                      hx-include="#@{FeatureName}ListQuery.StateId"
                      hx-vals='{"Page":"1"}'
                      hx-target="#feature-details"
                      hx-push-url="true">
                    <div>
                        <label class="form-label" for="{featurename}-search">Search</label>
                        <input class="form-control" id="{featurename}-search" name="Search"
                               value="@Model.Search.Search" placeholder="Search...">
                    </div>
                    <div class="d-flex gap-2">
                        <button class="btn btn-dark" type="submit">Apply</button>
                        <button class="btn btn-outline-secondary" type="button"
                                hx-get="/{route}/list"
                                hx-include="#@{FeatureName}ListQuery.StateId"
                                hx-vals='{"Page":"1"}'
                                hx-target="#feature-details"
                                hx-push-url="true">Reset</button>
                    </div>
                </form>

                <button class="btn btn-outline-dark" type="button"
                        hx-get="/{route}/create"
                        hx-target="#feature-details"
                        hx-push-url="true"
                        hx-params="none">Create new</button>
            </div>
        </div>
    </div>

    @if (!string.IsNullOrWhiteSpace(Model.Search.Search))
    {
        <div class="d-flex align-items-center gap-2 mb-3">
            <span class="text-muted">Search: @Model.Search.Search</span>
            <button class="btn btn-sm btn-outline-secondary" type="button"
                    hx-get="/{route}/list"
                    hx-include="#@{FeatureName}ListQuery.StateId"
                    hx-vals='{"Page":"1"}'
                    hx-target="#feature-details"
                    hx-push-url="true">Clear search</button>
        </div>
    }

    @await RenderPartialAsync(_{FeatureName}List.Create(Model))
</article>
```

### `Features/{FeatureName}/Slices/_{FeatureName}List.cshtml`
Table with sort headers, rows, and pager — this is the **fragment** returned by the list endpoint:
```razor
@inherits RazorSlice<{FeatureName}ListModel>
@using For = {RootNamespace}.HtmlNames.{RootNamespace}.Features.{FeatureName}.Models.{Entity}UpsertRequestHtml

@{
    var header = Model.SortHeader("/{route}/list");
}

<section class="section-panel">
    <div class="mb-2">
        <p class="text-muted mb-0">
            @if (Model.Total == 0)
            { <text>No items found.</text> }
            else
            { <text>Showing @Model.FromItem–@Model.ToItem of @Model.Total items.</text> }
        </p>
    </div>

    <form class="list-request-form"
          hx-get="/{route}/list"
          hx-include="#@{FeatureName}SearchModel.StateId, #@{FeatureName}ListQuery.StateId"
          hx-target="#feature-details"
          hx-push-url="true">
        <div class="card border-0 shadow-sm overflow-hidden">
            <div class="table-responsive">
                <table class="table table-hover mb-0">
                    <thead>
                    <tr>
                        <th>@header.Render(For.{Column}.Name, "{Label}")</th>
                        @* repeat for each column *@
                        <th class="text-end">Actions</th>
                    </tr>
                    </thead>
                    <tbody>
                    @foreach (var row in Model.Items)
                    {
                        <tr>
                            <td>@row.{Column}</td>
                            @* repeat for each column *@
                            <td>
                                <div class="d-flex justify-content-end gap-1">
                                    <button class="btn btn-sm btn-outline-secondary" type="button"
                                            hx-get="/{route}/edit/@row.Id"
                                            hx-target="#feature-details"
                                            hx-push-url="true"
                                            hx-params="none">Edit</button>
                                    <button class="btn btn-sm btn-outline-danger" type="button"
                                            hx-get="/{route}/delete/@row.Id"
                                            hx-target="#dialog-host">Delete</button>
                                </div>
                            </td>
                        </tr>
                    }
                    </tbody>
                </table>
            </div>
        </div>

        <div class="mt-3">
            @await RenderPartialAsync(_Pager.Create(Model.ToPagerModel("/{route}/list")))
        </div>
    </form>
</section>
```

### Slice hierarchy

```
_FeaturePage.cshtml      ← state serialization + #feature-details refresh wrapper
  └─ _ListPage.cshtml    ← toolbar (search + create) + renders list partial
       └─ _{Name}List.cshtml  ← table + sort headers + pager
```

- **State** is serialized in `_FeaturePage` as hidden divs
- **`#feature-details`** has `hx-trigger="{Name}.ListRefresh from:body"` — refreshes the full list
- **Toolbar** (search form) stays stable — it's inside `_ListPage` which is only re-rendered on full page load
- The list endpoint returns `_ListPage` as `.AsFragment()` — the whole list area swaps on HTMX requests

## Update existing files:

### Update `{FeatureName}ContentService.cs`
```csharp
public NavigationRouteItem CreateNavigationItem() => new(
    "{featurename}",
    "{FeatureName}",
    "/{route}",
    new PageDefinition(httpContext =>
    {
        var search = new {FeatureName}SearchModel(
            httpContext.Request.Query["Search"].ToString() is { Length: > 0 } s ? s : null);
        var query = {FeatureName}ListQuery.FromQuery(httpContext.Request.Query);
        return _FeaturePage.Create(CreateListModel(search, query));
    }),
    Order: {order});

public {FeatureName}ListModel CreateListModel({FeatureName}SearchModel search, {FeatureName}ListQuery query)
{
    // filter, then paginate
    var paged = items.AsQueryable().ToPagedList(query);
    return new {FeatureName}ListModel(paged.Total, search, query, paged.Items);
}
```

### Update `{FeatureName}Endpoints.cs`
```csharp
app.MapGet("/{route}/list", ([AsParameters] {FeatureName}SearchModel search, {FeatureName}ListQuery query) =>
{
    var model = _content.CreateListModel(search, query);
    return Result.For(_FeaturePage.Create(model))
        .AsFragment(_ListPage.Create(model))
        .WithState(search)
        .WithState(query)
        .BuildAsync();
});
```

Note: `.AsFragment(_ListPage)` — the fragment is the list page (toolbar + table), not just the table. The `_FeaturePage` wrapper with state serialization is only sent on full page loads.

## Key concepts:

### Sort headers (`SortHeader`)
- `Model.SortHeader("/{route}/list")` creates a sort context with Bootstrap defaults
- `header.Render("fieldName", "Label")` renders a clickable button with sort direction indicator
- The field name must match a property on the row model (auto-generated SortMap)

### Pager (`_Pager` slice)
- `Model.ToPagerModel("/{route}/list")` creates a `PagerModel` for the built-in `_Pager` slice
- `_Pager` renders Bootstrap pagination with page numbers and page size selector

### `ListRequest<T>` base class
- Provides `Page`, `PageSize`, `SortBy`, `SortDirection` properties
- Source generator auto-creates `SortMap`, `FromQuery()`, `BindAsync()`, and `IHasState` implementation
- Use `.ToPagedList(query)` to sort and paginate `IQueryable<T>`

## Important:
- `ListRequest<T>` is `partial` — generator auto-creates `SortMap` from row model properties
- Use `[SortDisable]` on properties you don't want sortable (e.g., Id)
- `.WithState(search).WithState(query)` sends OOB state updates on fragment responses
- `hx-include` references state by `#@{Model}SearchModel.StateId, #@{Model}ListQuery.StateId`
- Cancel buttons on create/edit forms should `hx-include` state IDs to preserve list position
