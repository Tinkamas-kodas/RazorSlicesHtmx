# Apskaita.Web — Copilot Instructions

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
  Slices/                                 ← Layout, Page, _TopNav
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
Result.Page(_PageSlice.Create(model)).Build()

// HTMX fragment (detail swap only):
Result.Page(_PageSlice.Create(model))).Build()

// With toast notification:
Result.Page(_Empty.Create()).WithToast("Saved!").Build()
Result.Page(_Empty.Create()).WithToast("Deleted", title: "Done", tone: ToastTone.Warning).Build()

// With HTMX trigger:
Result.Page(_Empty.Create()).WithTrigger("Items.ListRefresh").Build()

// With state preservation:
Result.Page(page).WithState(search).WithState(query).Build()

// With retarget/reswap (override where/how response is swapped):
Result.Page(detail).WithRetarget("#edit-form").WithReswap(HtmxSwap.OuterHtml).Build()

// With CancellationToken:
Result.Page(detail).Build()

// Override page title for this response:
Result.Page(detail).WithPageTitle("Edit item").Build()
```

### Page title defaults

- If `WithPageTitle(...)` is not called, page title defaults to `NavigationItem.Label`.
- For HTMX responses, title is sent as OOB `<title hx-swap-oob="true">...</title>`.
- For full-page responses, title flows through `FeatureShellContext.PageTitle` to the app renderer.

### Fragment-only endpoints (no full-page fallback):
```csharp
return HtmxFragmentResult.Create(fragment)
    .WithTrigger("Items.ListRefresh")
    .WithToast("Done")
    .Build(); // sync, not async
```

## Navigation Model

```
NavigationItem (record)
└── NavigationGroupItem — non-navigable group with Children
```

- `NavigationItem(key, label, route, Order, AuthorizationPolicy?)` — navigable leaf with route
- `NavigationGroupItem(key, label, children, Order, AuthorizationPolicy?)` — folder/group
- Page rendering is done by mapping the route in `MapEndpoints` — there is no `PageDefinition`

### Grouped navigation
```csharp
public override IReadOnlyList<NavigationItem> NavigationItems =>
[
    new NavigationGroupItem("admin", "Administration",
    [
        new NavigationItem("users", "Users", "/admin/users"),
        new NavigationItem("roles", "Roles", "/admin/roles")
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
app.MapGet("/items/list", (HttpContext ctx, [AsParameters] ItemSearchModel search, ItemListQuery query) =>
{
    var model = service.CreateListModel(search, query);
    if (ctx.IsHtmxTarget("items-list"))
        return Result.Page(_ItemsList.Create(model)).WithState(search).WithState(query);
    return Result.Page(_ListPage.Create(model))
        .WithWorkspace<ItemsWorkspaceFactory>()
        .WithState(search)
        .WithState(query);
});
```

### Workspace slice — injects HtmxStateContext, serializes state
```cshtml
@inherits RazorSlice<RazorSlice>
@inject HtmxStateContext StateContext

<div id="items-workspace">
    @(StateContext.Serialize<ItemSearchModel>(ItemSearchModel.StateId))
    @(StateContext.Serialize<ItemListQuery>(ItemListQuery.StateId))
    <div id="feature-details" ...>@await RenderPartialAsync(Model)</div>
</div>
```

### Workspace factory
```csharp
public sealed class ItemsWorkspaceFactory() : BaseWorkspaceFactory("feature-details")
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}
```

### HTMX include/target for list forms (HTMX 4)
```cshtml
<form class="list-request-form"
    hx-get="/items/list"
    hx-include:inherited="#@ItemSearchModel.StateId, #@ItemListQuery.StateId"
    hx-target:inherited="#items-list"
    hx-push-url="true">
```

HTMX 4 uses explicit inheritance by default. Keep direct `hx-target` on the triggering element when needed, but do not mix `hx-target` and `hx-target:inherited` on the same element.

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

The generator creates:
1. **Static accessor class** (`ItemUpsertRequestHtml`) — metadata only, each accessor implements `IPropertyAccessor`
2. **`.For()` extension method** — returns a context struct with both metadata and values from a model instance
3. **`FieldContext<T>`** — struct implementing `IPropertyAccessor` with `.Value`, `.HasErrors`, `.FieldErrors`

**Static accessors** (metadata only, for sort headers and labels):
```cshtml
@using For = Apskaita.Web.HtmlNames.Apskaita.Web.Features.Items.Models.ItemUpsertRequestHtml

<label for="@For.Code.Id">Code</label>
<input id="@For.Code.Id" name="@For.Code.Name" value="@Model.Code">
<th>@header.Render(For.Code.Name, "Code")</th>
```

**`.For()` context** (metadata + values from model instance):
```cshtml
@using Apskaita.Web.HtmlNames.Apskaita.Web.Features.Items.Models

@{ var f = Model.Request.For(); }

<input id="@f.Code.Id" name="@f.Code.Name" value="@f.Code.Value">
<input id="@f.Name.Id" name="@f.Name.Name" value="@f.Name.Value">
```

**`.For()` with validation errors** (pass `IHaveValidationResult.Errors`):
```cshtml
@{ var f = Model.For(Model.Errors); }
@* f.Code.HasErrors, f.Code.FieldErrors are now populated *@
```

**Bootstrap 5 form helpers** (from `RazorSlicesHtmx.Bootstrap5`):
```cshtml
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.For(Model.Errors); }

@f.Code.Label("Code")
@f.Code.Input(placeholder: "ITEM-001", autoFocus: true)
@f.Code.Validation()

@f.Name.Label("Name")
@f.Name.Input(placeholder: "Item name")
@f.Name.Validation()

@f.IsActive.Checkbox(label: "Active")
@f.Description.TextArea(placeholder: "Describe the item")
```

Available helpers: `.Label(text)`, `.Input()`, `.TextArea()`, `.Checkbox(label?)`, `.Select(options)`, `.Validation()`.
All accept optional `BootstrapFieldOptions` to override CSS per-call.
Global CSS is set via `BootstrapFieldOptions.Current` (configured through `AddRazorSlicesHtmxBootstrap5`).

**Generic helpers** with `FieldContext<T>` / `IPropertyAccessor`:
```csharp
void RenderField<T>(FieldContext<T> field, string label) { /* field.Name, .Id, .Value */ }
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
Result.Page(detail).WithRetarget("#edit-form").Build();
```

### `WithReswap(swapMode)` — override swap strategy
```csharp
Result.Page(detail).WithReswap(HtmxSwap.BeforeEnd).Build();
```

### Combined — validation error pattern
```csharp
Result.Page(detail)
    .WithRetarget("#edit-form").WithReswap(HtmxSwap.OuterHtml).Build();
```

Also available on `HtmxFragmentResult`:
```csharp
HtmxFragmentResult.Create(form).WithRetarget("#edit-form").WithReswap(HtmxSwap.OuterHtml).Build();
```

## Authorization

Each `NavigationItem` can declare an `AuthorizationPolicy`:
```csharp
new NavigationItem("admin-users", "Users", "/admin/users",
    AuthorizationPolicy: "AdminOnly")
```

- Endpoint auth: `.RequireAuthorization(policy)` on endpoint registrations.
- Navigation filtering: `Build()` automatically filters navigation tree via `IAuthorizationService`. Groups with no authorized children are excluded.
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
- For server-triggered list refresh, use `hx-trigger="{Feature}.ListRefresh from:document"` on `#feature-details`
- For shell events (`show-toast`, `show-dialog`, `dialog:clear`), listen with `document.addEventListener(...)` (not `document.body`) for htmx 4 reliability after cleanup/swap
- In mixed `HX-Trigger` JSON payloads, simple events should use `{}` payloads (avoid `null`)

## Rules

- Never create controllers — use minimal API endpoints in `MapEndpoints`
- Never reference `Microsoft.AspNetCore.Mvc` — use RazorSlices only
- All endpoints return `Task<IResult>` via `Build()`
- Models are `sealed record` types
- `[GenerateHtmlNames]` attribute on form request models (source-generated)
- State models must be `partial` for generator support
- All POST endpoints should use antiforgery tokens with the shared HTMX header adapter; do not disable antiforgery by default.

## Available Prompt Skills

Use these via Copilot chat:
- `/create-feature` — scaffold a new feature module
- `/add-list` — add a list page with sortable headers and pager
- `/add-form` — add a create/edit form to a feature
- `/add-endpoint` — add a new HTMX endpoint to a feature
