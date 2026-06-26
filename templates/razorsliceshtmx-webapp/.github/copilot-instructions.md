# RshtmxApp — Copilot Instructions

This is an ASP.NET Core web application built with **RazorSlicesHtmx** — server-rendered HTMX with RazorSlices and automatic feature discovery.

## Architecture

```
Features/
  {FeatureName}/
    Endpoints/{FeatureName}Endpoints.cs   ← IFeatureModule, routes
    Models/                               ← records for slice models
    Services/{FeatureName}ContentService.cs ← NavigationItem, data
    Slices/                               ← .cshtml RazorSlice views
    Validators/                           ← FluentValidation (optional)
Shared/
  Models/                                 ← layout models
  Rendering/                              ← IFeaturePageRenderer
  Slices/                                 ← Layout, Page, _SidebarNav
```

## Feature Module

Every feature extends `BaseFeatureModule(FeatureResultBuilder)`:
```csharp
public sealed class MyEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems => [...];
    public override void MapEndpoints(WebApplication app) { ... }
}
```

Features are auto-discovered by `FeatureRegistry.Discover()` — no manual registration needed.

## Building Results

Always use `FeatureResultBuilder` — never return raw `IResult`:
```csharp
// Full page (detail swap + navigation update):
Result.For(_PageSlice.Create(model)).BuildAsync()

// HTMX fragment (detail swap only):
Result.For(_PageSlice.Create(model)).AsFragment(_FragmentSlice.Create(model)).BuildAsync()

// With toast notification:
Result.For(_Empty.Create()).WithToast("Saved!").BuildAsync()
Result.For(_Empty.Create()).WithToast("Deleted", title: "Done", tone: ToastTone.Warning).BuildAsync()

// With HTMX trigger:
Result.For(_Empty.Create()).WithTrigger("Items.ListRefresh").BuildAsync()

// With state preservation:
Result.For(page).AsFragment(list).WithState(search).WithState(query).BuildAsync()

// With retarget/reswap (override where/how response is swapped):
Result.For(detail).AsFragment(form).WithRetarget("#edit-form").WithReswap(HtmxSwap.OuterHtml).BuildAsync()

// With CancellationToken:
Result.For(detail).AsFragment(fragment).BuildAsync(cancellationToken)
```

### Fragment-only endpoints (no full-page fallback):
```csharp
return HtmxFragmentResult.Create(fragment)
    .WithTrigger("Items.ListRefresh")
    .WithToast("Done")
    .Build(); // sync, not async
```

## Navigation Model

```
NavigationItem (abstract)
├── NavigationRouteItem — navigable leaf with Route + PageDefinition
└── NavigationGroupItem — non-navigable group with Children
```

- `NavigationRouteItem(key, label, route, pageDefinition, Order, AuthorizationPolicy?)` — leaf with route
- `NavigationGroupItem(key, label, children, Order, AuthorizationPolicy?)` — folder/group
- `PageDefinition(() => slice)` — lazy factory for full-page rendering

### Grouped navigation
```csharp
public override IReadOnlyList<NavigationItem> NavigationItems =>
[
    new NavigationGroupItem("admin", "Administration",
    [
        new NavigationRouteItem("users", "Users", "/admin/users", usersPage),
        new NavigationRouteItem("roles", "Roles", "/admin/roles", rolesPage)
    ])
];
```

Groups with no authorized children are automatically excluded.

### Multi-assembly discovery
```csharp
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover([typeof(Program).Assembly, typeof(SharedFeatures).Assembly], sp));
```

## State Management

State is persisted via hidden-field divs across HTMX interactions. Any `partial` class or record passed to `WithState(...)` gets an `IHasState` implementation generated automatically.

### Declaring state
```csharp
// Search/filter state
public sealed partial record ItemSearchModel(string? Search);

// Paging/sort state (via ListRequest<T>)
public sealed partial class ItemListQuery : ListRequest<ItemRowModel> { ... }
```

### Generated members
```csharp
public const string StateId = "item-search-model";  // kebab-case, compile-time constant
public IHtmlContent Serialize();      // <div id="..."><input ...></div>
public IHtmlContent SerializeOob();   // same with hx-swap-oob
```

### Endpoint usage
```csharp
app.MapGet("/items/list", ([AsParameters] ItemSearchModel search, ItemListQuery query, AppDbContext db) =>
{
    var model = service.CreateListModel(db, search, query);
    return Result.For(_FeaturePage.Create(model))
        .AsFragment(_ListPage.Create(model))
        .WithState(search)
        .WithState(query)
        .BuildAsync();
});
```

### Host slice — render initial hidden divs
```cshtml
@Model.Search.Serialize()
@Model.List.Serialize()
```

### HTMX include — reference by StateId
```cshtml
hx-include="#@ItemSearchModel.StateId, #@ItemListQuery.StateId"
```

### Exclude properties
```csharp
public sealed partial record ItemSearchModel(
    string? Search,
    [property: StateNotMap] string? InternalToken);
```

## List Features

### `ListRequest<T>` — paging/sort base class

Subclass with `partial`; the generator adds `FromQuery`, `BindAsync`, and `SortMap`:

```csharp
public sealed partial class ItemListQuery : ListRequest<ItemRowModel>
{
    private const string DefaultSortBy = "code";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 5;

    public ItemListQuery()
    {
        SortBy = DefaultSortBy;
        Page = DefaultPage;
        PageSize = DefaultPageSize;
    }
}
```

Properties: `Page`, `PageSize`, `SortBy`, `SortDirection`.

### Sort Map Customization

Attributes on row model properties:
- `[SortDisable]` — exclude from sort keys
- `[CustomSortExpression(nameof(Method))]` — custom sort expression

```csharp
public sealed record ItemRowModel(
    [property: SortDisable] int Id,
    string Code,
    string Name,
    [property: CustomSortExpression(nameof(IsEnabledSort))] bool IsEnabled)
{
    public static Expression<Func<ItemRowModel, object?>> IsEnabledSort() => row => !row.IsEnabled;
}
```

### `ListResponse<T, TRequest>` — paged output

Constructed by `ToPagedList`. Exposes: `Total`, `TotalPages`, `CurrentPage`, `FromItem`, `ToItem`, `Items`, `Request`.

```csharp
public sealed record ItemListModel(int Total, ItemSearchModel Search, ItemListQuery Request, List<ItemRowModel> Items)
    : ListResponse<ItemRowModel, ItemListQuery>(Total, Request, Items);
```

### Filter and paginate
```csharp
var items = db.Items.AsNoTracking()
    .Select(item => new ItemRowModel(item.Id, item.Code, item.Name, item.IsEnabled));

if (!string.IsNullOrWhiteSpace(search.Search))
    items = items.Where(row => row.Code.Contains(search.Search) || row.Name.Contains(search.Search));

var paged = items.ToPagedList(query);
return new ItemListModel(paged.Total, search, query, paged.Items);
```

### Sort headers — `SortHeader`

```cshtml
@{
    var header = Model.SortHeader("/items/list", BootstrapHeaderOptions.Default with
    {
        SortButtonClass = "my-sort-btn btn btn-link p-0",
    });
}

<th>@header.Render("code", "Code")</th>
<th>@header.Render("name", "Name")</th>
```

Or with slices:
```cshtml
<th>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("code", "Code")))</th>
```

Custom renderer: implement `IListSortHeaderRenderer` and pass to `Model.SortHeader(url, renderer)`.

### Pager — `_Pager` slice

```cshtml
@await RenderPartialAsync(_Pager.Create(Model.ToPagerModel("/items/list")))
```

With translations:
```cshtml
@await RenderPartialAsync(_Pager.Create(
    Model.ToPagerModel("/items/list", new PagerTexts(Showing: "Rodomi", Of: "iš", NoItems: "Nėra įrašų"))))
```

Override page sizes:
```csharp
Model.ToPagerModel("/items/list") with { PageSizeOptions = [10, 25, 50, 100] }
```

Programmatic:
```cshtml
@Model.PagerContext("/items/list", BootstrapPagerOptions.Default with { PagerContainerClass = "my-pager" }).Render()
```

### How To Build a New List Feature

1. Create a row model for table rows.
2. Create a `partial` class deriving from `ListRequest<TRow>`; define `DefaultSortBy`, `DefaultPage`, `DefaultPageSize`.
3. Create a `partial` record for search/filter state.
4. In the endpoint, take both as parameters (`[AsParameters] SearchModel search, ListQuery query`).
5. Filter in the service, then call `items.ToPagedList(query)`.
6. Return `.WithState(search).WithState(query)`.
7. In the host slice, render `@search.Serialize()` and `@query.Serialize()`.
8. Use `Model.SortHeader(url, BootstrapHeaderOptions.Default)` and `header.Render(column, label)` per column.
9. Render pager via `_Pager.Create(Model.ToPagerModel(url))`.
10. In HTMX, use `hx-include="#@SearchModel.StateId, #@ListQuery.StateId"`.

## Source Generators

### `[GenerateHtmlNames]` — form field metadata

```csharp
[GenerateHtmlNames]
public sealed class ItemUpsertRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}
```

The generator creates a companion class with `const string` properties per field:

```csharp
// Generated: RshtmxApp.HtmlNames.RshtmxApp.Features.Items.Models.ItemUpsertRequestHtml
public static class ItemUpsertRequestHtml
{
    public static class Code
    {
        public const string Name = "Code";   // for <input name="...">
        public const string Id = "Code";     // for <input id="..."> and <label for="...">
        public const string Path = "Code";   // for nested models
    }
    public static class Name { ... }
}
```

Usage in Razor:
```cshtml
@using For = RshtmxApp.HtmlNames.RshtmxApp.Features.Items.Models.ItemUpsertRequestHtml

<label for="@For.Code.Id">Code</label>
<input id="@For.Code.Id" name="@For.Code.Name" value="@Model.Code">
```

These `const string` values are also used in sort headers — `For.Code.Name` is the column key for `header.Render(For.Code.Name, "Code")`:
```cshtml
<th>@header.Render(For.Code.Name, "Code")</th>
```

### State generator — auto for `partial` types passed to `WithState()`

No attribute needed. Generates `StateId`, `Serialize()`, `SerializeOob()`.

### List request binder — auto for `partial` classes extending `ListRequest<T>`

Generates `FromQuery`, `BindAsync`, `SortMap`.

## Configuration — `RazorSlicesHtmxOptions`

Settings: `DetailTargetSelector`, `DialogHostSelector`, `DialogClass`, `ToastHostSelector`, `ToastDelayMilliseconds`, `DefaultOobSwap`.

```csharp
builder.Services.Configure<RazorSlicesHtmxOptions>(
    builder.Configuration.GetSection("RazorSlicesHtmx"));
```

```json
{
  "RazorSlicesHtmx": {
    "DetailTargetSelector": "#details-pane",
    "DialogHostSelector": "#dialog-host",
    "ToastHostSelector": "#toast-host",
    "ToastDelayMilliseconds": 3200
  }
}
```

## Response Target and Swap Override

### `WithRetarget(selector)` — override target element
```csharp
Result.For(detail).AsFragment(formWithErrors).WithRetarget("#edit-form").BuildAsync();
```

### `WithReswap(swapMode)` — override swap strategy
```csharp
Result.For(detail).AsFragment(item).WithReswap(HtmxSwap.BeforeEnd).BuildAsync();
```

### Combined — validation error pattern
```csharp
Result.For(detail).AsFragment(formWithErrors)
    .WithRetarget("#edit-form").WithReswap(HtmxSwap.OuterHtml).BuildAsync();
```

Also available on `HtmxFragmentResult`:
```csharp
HtmxFragmentResult.Create(form).WithRetarget("#edit-form").WithReswap(HtmxSwap.OuterHtml).Build();
```

## Authorization

Each `NavigationItem` can declare an `AuthorizationPolicy`:
```csharp
new NavigationRouteItem("admin-users", "Users", "/admin/users", usersPage,
    AuthorizationPolicy: "AdminOnly")
```

- Endpoint auth: `.RequireAuthorization(policy)` on endpoint registrations.
- Navigation filtering: `BuildAsync()` automatically filters navigation tree via `IAuthorizationService`. Groups with no authorized children are excluded.
- No auth: when `IAuthorizationService` is not registered, all items pass through.

## HTMX Error Handling

```csharp
app.UseHtmxErrorHandling();
```

- HTMX request + exception → error toast with `ToastTone.Error`
- HTMX request + 4xx/5xx → status-appropriate toast
- Non-HTMX request → passes through unaffected
- Status reset to 200 so HTMX processes the response

Custom messages:
```csharp
app.UseHtmxErrorHandling(options =>
{
    options.FormatTitle = (statusCode, ex) => statusCode == 404 ? "Not Found" : "Error";
    options.FormatMessage = (statusCode, ex) => statusCode == 404 ? "Page not found." : "An unexpected error occurred.";
});
```

## ToastTone

Semantic enum: `Info`, `Success`, `Warning`, `Error`.

| ToastTone | Bootstrap5 CSS |
|-----------|---------------|
| `Info` | `text-bg-info` |
| `Success` | `text-bg-success` |
| `Warning` | `text-bg-warning` |
| `Error` | `text-bg-danger` |

```csharp
.WithToast("Item saved")  // default: Success
.WithToast("Deleted", title: "Done", tone: ToastTone.Warning)
```

## FluentValidation Integration

Transform validation results to error dictionaries:
```csharp
ValidationResult result = validator.Validate(request);
var errors = result.ToErrorDictionary();
```

Bootstrap5 validation UI in Razor:
```cshtml
@await RenderPartialAsync(_ValidationMessage.Create(new ValidationMessageModel(For.Name.Name, Model.Errors)))
```

## RazorSlice Conventions

- File names start with `_` (e.g., `_ListPage.cshtml`)
- Use `@inherits RazorSlice<TModel>` for typed models
- Use `@inherits RazorSlice` for no-model slices
- Each feature's Slices/ folder has `_ViewImports.cshtml` with model namespace

## HTMX Conventions

- `hx-get`/`hx-post` point to feature routes
- `hx-target` uses `detailTargetSelector` from options for detail pane
- `hx-swap` uses `HtmxSwap.InnerHtml` or `HtmxSwap.OuterHtml`
- `hx-push-url="true"` for navigation links
- `hx-include` references state models by `StateId`

## Rules

- Never create controllers — use minimal API endpoints in `MapEndpoints`
- Never reference `Microsoft.AspNetCore.Mvc` — use RazorSlices only
- All endpoints return `Task<IResult>` via `BuildAsync()`
- Models are `sealed record` types
- `[GenerateHtmlNames]` attribute on form request models (source-generated)
- State models must be `partial` for generator support
- All POST endpoints need `.DisableAntiforgery()` (HTMX doesn't send antiforgery tokens)

## Available Prompt Skills

Use these via Copilot chat:
- `/create-feature` — scaffold a new feature module
- `/add-list` — add a list page with sortable headers and pager
- `/add-form` — add a create/edit form to a feature
- `/add-endpoint` — add a new HTMX endpoint to a feature
