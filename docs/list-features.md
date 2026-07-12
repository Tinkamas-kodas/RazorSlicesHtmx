# List Features

A structured paging and sorting workflow built around `ListRequest<T>` and `ListResponse<T, TListRequest>`.

## ListRequest&lt;T&gt;

Subclass `ListRequest<T>` with `partial`. The generator adds `FromQuery`, `BindAsync`, and `SortMap`:

```csharp
public sealed partial class ItemListQuery : ListRequest<ItemRowModel>
{
    private const string DefaultSortBy = "code";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 5;
    // private const int MaxPageSize = 100; // optional cap
}
```

`T` is the row model type. `SortMap` keys are derived from its public properties.

## Sort map customization

Apply attributes on row model properties:

```csharp
public sealed record ItemRowModel(
    [property: SortDisable] int Id,          // excluded from sort keys
    string Code,
    string Name,
    [property: CustomSortExpression(nameof(IsEnabledSortExpression))] bool IsEnabled)
{
    public static Expression<Func<ItemRowModel, object?>> IsEnabledSortExpression() =>
        row => !row.IsEnabled;
}
```

| Attribute | Effect |
|-----------|--------|
| `[SortDisable]` | Excludes property from sort keys |
| `[CustomSortExpression(nameof(Method))]` | Delegates sort expression to a static method |

## Building a list in the service

Apply filters first, then call `ToPagedList(query)`:

```csharp
public ItemListModel CreateListModel(AppDbContext db, ItemSearchModel search, ItemListQuery query)
{
    var items = db.Items.AsNoTracking()
        .Select(item => new ItemRowModel(item.Id, item.Code, item.Name, item.IsEnabled));

    if (!string.IsNullOrWhiteSpace(search.Search))
    {
        var term = search.Search.Trim();
        items = items.Where(r => r.Code.Contains(term) || r.Name.Contains(term));
    }

    var paged = items.ToPagedList(query);
    return new ItemListModel(paged.Total, search, query, paged.Items);
}
```

`ToPagedList` handles sorting and paging. Filter before calling it.

## ListResponse&lt;T, TListRequest&gt;

Returned by `ToPagedList`. Exposes: `Total`, `TotalPages`, `CurrentPage`, `FromItem`, `ToItem`, `Items`, `Request`.

## Search model binding

Simple `partial` records for search bind from query string via `[AsParameters]`:

```csharp
app.MapGet("/items/list", ([AsParameters] ItemSearchModel search, ItemListQuery query, AppDbContext db) => ...);
```

`[AsParameters]` unwraps the record. HTMX GET requests append `hx-include` values as query params, so this works for both URL navigation and HTMX interactions.

## Sort headers

Obtain a `ListSortHeaderContext` from `ListResponse` via `SortHeader(url, options?)`:

```cshtml
@{
    var header = Model.SortHeader("/items/list", BootstrapHeaderOptions.Default with
    {
        SortButtonClass = "btn btn-link p-0"
    });
}

{{-- Razor slice --}}
<th>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("code", "Code")))</th>
<th>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("name", "Name")))</th>

{{-- Programmatic (returns IHtmlContent) --}}
<th>@header.Render("code", "Code")</th>
<th>@header.Render("name", "Name")</th>
```

Each sort header renders a `<button>` with `hx-get`, `hx-push-url="true"`, and `hx-vals` for toggled `SortBy`/`SortDirection`. `hx-include` is intentionally omitted — it is inherited from an enclosing `<form hx-include="...">`.

Custom renderer:

```csharp
Model.SortHeader("/items/list", new MyHeaderRenderer()) // implements IListSortHeaderRenderer
```

Custom slice — write a slice that accepts `SortButtonModel` directly:

```cshtml
@inherits RazorSlice<SortButtonModel>
<button hx-get="@Model.GetUrl" hx-push-url="true"
        hx-vals='{"SortBy":"@Model.Column","SortDirection":"@Model.NextDirection"}'>
    @Model.Label @(Model.State == SortState.SortedAsc ? "▲" : Model.State == SortState.SortedDesc ? "▼" : "⇅")
</button>
```

## Pager

```cshtml
{{-- Razor slice --}}
@await RenderPartialAsync(_Pager.Create(Model.ToPagerModel("/items/list")))

{{-- With localized text --}}
@await RenderPartialAsync(_Pager.Create(
    Model.ToPagerModel("/items/list", new PagerTexts(Showing: "Rodomi", Of: "iš", NoItems: "Nėra įrašų"))))

{{-- Override page size options --}}
@await RenderPartialAsync(_Pager.Create(
    Model.ToPagerModel("/items/list") with { PageSizeOptions = [10, 25, 50, 100] }))

{{-- Programmatic --}}
@Model.PagerContext("/items/list").Render()

{{-- With CSS overrides --}}
@Model.PagerContext("/items/list",
    BootstrapPagerOptions.Default with { PagerActiveClass = "btn btn-primary btn-sm" }).Render()
```

Pager behaviour:
- **Page windowing**: always shows first, last, current, ±1 pages. Ellipsis between non-consecutive entries. ≤5 pages: no ellipsis.
- **Page size selector**: uses `hx-vals='js:{"PageSize": event.target.value, "Page": 1}'` — resets to page 1.
- **Localisation**: all strings from `PagerTexts` (defaults to English).

## Full workflow checklist

1. Create row model record with sort attribute annotations.
2. Create `partial class` extending `ListRequest<TRow>` — define `DefaultSortBy`, `DefaultPage`, `DefaultPageSize`.
3. Create `partial record` for search state (e.g. `ItemSearchModel(string? Search)`).
4. Endpoint parameters: `([AsParameters] ItemSearchModel search, ItemListQuery query)`.
5. Service: filter → `items.ToPagedList(query)`.
6. Result builder: `.WithState(search).WithState(query)`.
7. Host slice: `@Model.Search.Serialize()` and `@Model.List.Serialize()`.
8. List slice: sort headers + pager.
9. HTMX: `hx-include="#@ItemSearchModel.StateId, #@ItemListQuery.StateId"`.

## See also

- [State](state.md)
- [Forms](forms.md)
- [Bootstrap5](bootstrap5.md)
- [Response Building](response-building.md)
