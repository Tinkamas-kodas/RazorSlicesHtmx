# RazorSlicesHtmx

`RazorSlicesHtmx` is a small server-rendered HTMX toolkit built around RazorSlices and feature discovery.

Licensed under the [MIT License](LICENSE).

See the [changelog](CHANGELOG.md) for release history.

The codebase is intentionally split so the core package handles feature registration and HTMX response composition, while application UI stays outside the core.

## Packages

### `RazorSlicesHtmx.AspNetCore`

Core package for:

- feature discovery and registration
- `IFeatureModule`, `BaseFeatureModule`, `NavigationItem`, `NavigationRouteItem`, `NavigationGroupItem`, `PageDefinition`, and `FeatureRegistry`
- hierarchical navigation model with ASP.NET Core authorization filtering
- HTMX response composition through `FeatureResultBuilder` (full-page feature responses) and `HtmxFragmentResult` (fragment-only responses)
- HTMX error handling middleware (`UseHtmxErrorHandling`)
- OOB fragments, triggers, navigation, and location responses
- `ToastTone` enum (`Info`, `Success`, `Warning`, `Error`) for UI-agnostic toast severity
- technical slices that are not tied to a CSS framework, such as `_Empty`, `HtmxFragment`, and `HtmxOob`
- app-facing contracts such as `FeatureShellContext`, `IFeaturePageRenderer`, and `ITransientUiRenderer`

Notably, this package does **not** own your app layout, branding, page shell, or navigation UI.

### `RazorSlicesHtmx.Bootstrap5`

Bootstrap adapter for transient UI only:

- dialog rendering
- toast rendering
- validation message partial
- Bootstrap-specific helper extensions
- Bootstrap-specific default settings registered through `PostConfigure<RazorSlicesHtmxOptions>`

This package is intentionally **not** a full app shell or admin template.

What it implements:

- `ITransientUiRenderer` for Bootstrap dialog and toast rendering
- Bootstrap validation message markup for field-level validation feedback
- Bootstrap-specific helper extensions such as `InputClass(...)`
- Bootstrap fallback defaults for `DialogClass` and `ToastDelayMilliseconds`
- `BootstrapHeaderOptions.Default` — Bootstrap CSS defaults for sort header buttons
- `BootstrapPagerOptions.Default` — Bootstrap CSS defaults for the pager
- `SortHeader(url, options?)` extension on `ListResponse` — Bootstrap-styled sort header context
- `PagerContext(url, options?, texts?)` extension on `ListResponse` — Bootstrap-styled pager context
- `_Pager` slice — Bootstrap pager with smart page windowing and page-size selector

How to use it:

```csharp
using RazorSlicesHtmx.Bootstrap5.Extensions;

builder.Services.AddRazorSlicesHtmxBootstrap5();
```

Optional override example:

```csharp
builder.Services.AddRazorSlicesHtmxBootstrap5(options =>
{
  options.DialogClass = "modal-dialog modal-dialog-centered modal-lg";
  options.ToastDelayMilliseconds = 3200;
});
```

Use this package when you want Bootstrap-styled dialogs, toasts, and validation messages, but still want the app to own the full page shell.

### `RazorSlicesHtmx.FluentValidation`

Validation integration package for:

- mapping `FluentValidation.Results.ValidationResult` into UI-friendly dictionaries

What it implements:

- `ToErrorDictionary()` for transforming `ValidationResult` into the error structure used by the view layer

How to use it:

```csharp
using FluentValidation.Results;
using RazorSlicesHtmx.FluentValidation.Extensions;

ValidationResult validationResult = validator.Validate(request);

var errors = validationResult.ToErrorDictionary();
```

This package is intentionally narrow. It is meant to transform FluentValidation errors, not to own validation UI rendering.

### `RazorSlicesHtmx.Generators`

Roslyn source generator package. Provides two generators:

**HTML names generator** — strongly typed HTML field metadata for forms:

- `[GenerateHtmlNames(maxDepth: 0)]` attribute (injected at compile time)
- generated constants grouped per field as `For.<Field>.Name`, `For.<Field>.Id`, and `For.<Field>.Path`
- compile-time safe form metadata without runtime expression parsing

```csharp
using RazorSlicesHtmx.HtmlNames;

[GenerateHtmlNames]
public sealed class ItemUpsertRequest
{
  public string? Code { get; set; }
  public string? Name { get; set; }
}
```

In Razor:

```csharp
@using For = RazorSlicesHtmx.Demo.HtmlNames.RazorSlicesHtmx.Demo.Features.Items.Models.ItemUpsertRequestHtml
```

```html
<label for="@For.Code.Id">Code</label>
<input id="@For.Code.Id" name="@For.Code.Name">
```

**State generator** — `IHasState` implementation for any `partial` type passed to `WithState(...)`:

- detects call sites automatically — no attribute required
- generates `const string StateId`, `Serialize()`, and `SerializeOob()` on the type
- see [State Management](#state-management) for usage

**List request binder generator** — `FromQuery` and `BindAsync` for `ListRequest<T>` subclasses:

- generated `SortMap` from row model public properties
- respects `[SortDisable]` and `[CustomSortExpression]`
- see [List Features](#list-features) for usage

NuGet packaging notes:

- package is shipped as an analyzer (`analyzers/dotnet/cs`)
- analyzer assembly is not included as a runtime `lib` dependency

## State Management

`RazorSlicesHtmx` uses hidden-field divs to persist HTMX request state across interactions. State and paging/sort are kept in separate models so each HTMX request can include exactly the state it needs.

### How It Works

Any `partial` class or record passed to `WithState(...)` gets an `IHasState` implementation generated automatically by the source generator. No attribute is required — the generator detects the type from the call site.

The generator produces three members on the type:

```csharp
public const string StateId = "item-search-model";   // type-level, no instance needed

public IHtmlContent Serialize();      // renders <div id="item-search-model"><input ...></div>
public IHtmlContent SerializeOob();   // renders the same div wrapped in hx-swap-oob
```

`StateId` is the type name in kebab-case. It is a compile-time constant, accessible directly from Razor without an instance.

### Declaring a State Model

Declare a `partial` record or class for each logical piece of state:

```csharp
// Search/filter state — separate from paging so each HTMX request controls what it carries.
public sealed partial record ItemSearchModel(string? Search);

// Paging and sort state — generated via ListRequest<T>, see List Features section.
public sealed partial class ItemListQuery : ListRequest<ItemRowModel> { ... }
```

The generator runs when `WithState(search)` and `WithState(query)` are detected in the endpoint.

### Endpoint

Call `WithState` once per state model. Chaining is supported:

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

On HTMX fragment responses, `WithState` sends each model as an OOB update so the hidden divs stay current in the DOM.

### Host Slice

Render the initial hidden divs once in the feature host slice (the one shown on full-page loads):

```cshtml
@Model.Search.Serialize()
@Model.List.Serialize()
```

### HTMX Include

Use `StateId` directly in `hx-include`. Because it is a `const`, no model instance is needed:

```cshtml
{{-- Sort/paging: preserve search, change sort or page --}}
hx-include="#@ItemSearchModel.StateId, #@ItemListQuery.StateId"

{{-- Search submit: include sort state only; page resets via hx-vals --}}
hx-include="#@ItemListQuery.StateId"
hx-vals='{"Page":"1"}'

{{-- Non-list actions (create/edit): no state in URL --}}
hx-params="none"
```

Separating search and list state means you never need a CSS `:is()` filter to selectively exclude fields — you just omit the state model you do not want.

### Excluding Properties

Mark a property with `[StateNotMap]` to exclude it from state serialization:

```csharp
public sealed partial record ItemSearchModel(
    string? Search,
    [property: StateNotMap] string? InternalToken);
```

---

## List Features

`RazorSlicesHtmx` provides a structured paging and sorting workflow built around `ListRequest<T>` and `ListResponse<T, TListRequest>`.

### `ListRequest<T>`

Base class for paging and sort request models. Subclass it with `partial` and the generator adds:

- `FromQuery(IQueryCollection query)` — reads paging and sort from query string
- `BindAsync(HttpContext, ParameterInfo)` — enables direct minimal API parameter binding
- `SortMap` — keyed sort expressions derived from public properties of the row type `T` (only when not manually overridden)

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

`DefaultSortBy`, `DefaultPage`, `DefaultPageSize`, and `MaxPageSize` constants are read by the generator to configure defaults and validation.

### Sort Map Customization

Apply attributes on row model properties to control generated sort keys:

- `[SortDisable]` — exclude the property from sort keys entirely
- `[CustomSortExpression(nameof(SomeMethod))]` — delegate to a static expression method

```csharp
using System.Linq.Expressions;

public sealed record ItemRowModel(
  [property: SortDisable] int Id,
  string Code,
  string Name,
  [property: CustomSortExpression(nameof(ItemRowModel.IsEnabledSortExpression))] bool IsEnabled)
{
  public static Expression<Func<ItemRowModel, object?>> IsEnabledSortExpression() =>
    row => !row.IsEnabled;
}
```

In this example `id` is not sortable and the `isenabled` key uses a custom expression.

### Filter and Sort in the Service

Search/filter logic belongs in the endpoint or service, not in the request model. Apply the filter first, then call `ToPagedList` — it handles sorting and paging:

```csharp
public ItemListModel CreateListModel(AppDbContext db, ItemSearchModel search, ItemListQuery query)
{
    var items = db.Items.AsNoTracking()
        .Select(item => new ItemRowModel(item.Id, item.Code, item.Name, item.IsEnabled));

    if (!string.IsNullOrWhiteSpace(search.Search))
    {
        var term = search.Search.Trim();
        items = items.Where(row => row.Code.Contains(term) || row.Name.Contains(term));
    }

    var paged = items.ToPagedList(query);
    return new ItemListModel(paged.Total, search, query, paged.Items);
}
```

### `ListResponse<T, TListRequest>`

Paged output record. Not generated — constructed by `ToPagedList`.

Exposes: `Total`, `TotalPages`, `CurrentPage`, `FromItem`, `ToItem`, `Items`, `Request`.

```csharp
var response = queryableRows.ToPagedList(listQuery);
```

### Sort Headers and Pager

Sort headers and the pager are deliberately separate so each can be customised or replaced independently.

#### Sort headers — `SortHeader`

Obtain a `ListSortHeaderContext` from any `ListResponse` via `SortHeader(url, options?)` (Bootstrap5 extension) or `SortHeader(url, renderer)` (custom renderer).

**Razor slice** — use `GetModel(column, label)` to obtain `SortButtonModel` and pass it to `_SortHeader`:

```cshtml
@{
    var header = Model.SortHeader("/items/list", BootstrapHeaderOptions.Default with
    {
        SortButtonClass = "my-sort-btn btn btn-link p-0",
        GlyphSpanClass  = "sort-glyph"
    });
}

<th>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("code",      "Code")))</th>
<th>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("name",      "Name")))</th>
<th>@await RenderPartialAsync(_SortHeader.Create(header.GetModel("isenabled", "Status")))</th>
```

**Programmatic renderer** — renders directly to `IHtmlContent`:

```cshtml
<th>@header.Render("code",      "Code")</th>
<th>@header.Render("name",      "Name")</th>
<th>@header.Render("isenabled", "Status")</th>
```

Both produce a `<button>` with `hx-get`, `hx-push-url="true"`, and `hx-vals` carrying the toggled `SortBy`/`SortDirection`. `hx-include` is intentionally omitted — it is inherited from the enclosing `<form hx-include="...">` element.

`BootstrapHeaderOptions` is a `record` — use `with` to override specific CSS fields:

```csharp
BootstrapHeaderOptions.Default with { SortButtonClass = "my-btn" }
```

**Custom renderer** — implement `IListSortHeaderRenderer` and pass it directly:

```csharp
Model.SortHeader("/items/list", new MyHeaderRenderer())
```

**Custom slice** — write your own slice that accepts `SortButtonModel` directly:

```cshtml
@* MyApp/Slices/_MySortHeader.cshtml *@
@inherits RazorSlice<SortButtonModel>

<button hx-get="@Model.GetUrl"
        hx-push-url="true"
        hx-vals='{"SortBy":"@Model.Column","SortDirection":"@Model.NextDirection"}'>
    @Model.Label @(Model.State == SortState.SortedAsc ? "▲" : Model.State == SortState.SortedDesc ? "▼" : "⇅")
</button>
```

```cshtml
<th>@await RenderPartialAsync(_MySortHeader.Create(header.GetModel("code", "Code")))</th>
```

#### Pager — `_Pager` slice or `PagerContext`

Obtain a `PagerModel` from any `ListResponse` via `ToPagerModel(url)`, or a `ListPagerContext` via `PagerContext(url, options?)`.

**Razor slice** — pass `PagerModel` directly to `_Pager`:

```cshtml
@await RenderPartialAsync(_Pager.Create(Model.ToPagerModel("/items/list")))
```

Supply translations via `PagerTexts`:

```cshtml
@await RenderPartialAsync(_Pager.Create(
    Model.ToPagerModel("/items/list", new PagerTexts(Showing: "Rodomi", Of: "iš", NoItems: "Nėra įrašų"))))
```

Override page size options (default `[5, 10, 20]`):

```csharp
Model.ToPagerModel("/items/list") with { PageSizeOptions = [10, 25, 50, 100] }
```

**Programmatic renderer** — returns `IHtmlContent`:

```cshtml
@Model.PagerContext("/items/list").Render()

@* With CSS overrides and translations: *@
@Model.PagerContext("/items/list",
    BootstrapPagerOptions.Default with { PagerContainerClass = "my-pager" },
    new PagerTexts(Showing: "Rodomi", Of: "iš")).Render()
```

Both produce a pager with smart page windowing, a page-size selector, and localised display text.

`BootstrapPagerOptions` is a `record` — use `with` to override specific CSS fields:

```csharp
BootstrapPagerOptions.Default with { PagerActiveClass = "btn btn-primary btn-sm" }
```

**Custom renderer** — implement `IListPagerRenderer` and pass it directly:

```csharp
Model.PagerContext("/items/list", new MyPagerRenderer()).Render()
```

**Custom slice** — write your own slice that accepts `PagerModel` directly:

```cshtml
@* MyApp/Slices/_MyPager.cshtml *@
@inherits RazorSlice<PagerModel>

@for (var p = 1; p <= Model.TotalPages; p++) { ... }
```

```cshtml
@await RenderPartialAsync(_MyPager.Create(Model.ToPagerModel("/items/list")))
```

#### Pager behaviour

- **Page windowing**: always shows first page, last page, current page, and one page on each side. Ellipsis (`…`) appears between non-consecutive entries. When the total number of pages is five or fewer all pages are shown without ellipsis.
- **Page size selector**: a `<select>` using `hx-vals='js:{"PageSize": event.target.value, "Page": 1}'` — resets to page 1 on change; avoids duplicate parameters.
- **Localisation**: all display strings (`Showing`, `of`, `No items`, `…`, `per page`) come from `PagerTexts` which defaults to English.

### Search Model Binding

Simple `partial` records for search/filter bind from query string via `[AsParameters]` without any generator:

```csharp
app.MapGet("/items/list", ([AsParameters] ItemSearchModel search, ItemListQuery query) => ...);
```

`[AsParameters]` unwraps the record and reads each property from the query string. Because HTMX GET requests append `hx-include` values as query parameters, this works for both direct URL navigation and HTMX-driven interactions.

### How To Build a New List Feature

1. Create a row model for table rows.
2. Create a `partial` class deriving from `ListRequest<TRow>`; define `DefaultSortBy`, `DefaultPage`, `DefaultPageSize` constants.
3. Create a `partial` record for search/filter state (e.g. `ItemSearchModel(string? Search)`).
4. In the endpoint, take both as parameters (`[AsParameters] ItemSearchModel search, ItemListQuery query`).
5. Apply filter explicitly in the service, then call `items.ToPagedList(query)` — it handles sorting and paging.
6. Return `.WithState(search).WithState(query)` in the result builder.
7. In the host slice, render `@Model.Search.Serialize()` and `@Model.List.Serialize()`.
8. In list slices, obtain a sort header context via `Model.SortHeader(url, BootstrapHeaderOptions.Default with { ... })` and call `header.Render(column, label)` per column.
9. Render the pager via `@await RenderPartialAsync(_Pager.Create(Model.ToPagerModel(url)))` or `Model.PagerContext(url).Render()`.
10. In HTMX requests, use `hx-include="#@ItemSearchModel.StateId, #@ItemListQuery.StateId"` (or a subset as needed).

### Demo app

The demo lives in `demo/RazorSlicesHtmx.Demo`.

The demo is responsible for:

- page layout
- page title and branding
- sidebar/navigation UI
- app-level page shell rendering
- feature examples and sample styling

## Architecture Boundaries

Current intended split:

- `AspNetCore`: HTMX orchestration and feature model
- `Bootstrap5`: transient UI adapter for dialogs, toasts, and validation partials
- `Generators`: compile-time HTML Name/Id/Path generation for request/view models
- app: full page shell and branding

That means the app provides its own page renderer and shell models, while the Bootstrap package only plugs into transient UI concerns.

## Core Contracts

The most important core contracts are:

- `IFeatureModule`
  feature registration unit; exposes `NavigationItems` (a list of `NavigationItem` nodes) and maps feature-local endpoints
- `BaseFeatureModule`
  abstract base class implementing `IFeatureModule`; provides a `Result.For(detail)` and `Result.Dialog(content)` convenience facade backed by `FeatureResultBuilder`; preferred over implementing `IFeatureModule` directly when using `FeatureResultBuilder`
- `NavigationItem`
  abstract base record for the navigation tree; has `Key`, `Label`, `Order`, and optional `AuthorizationPolicy`
- `NavigationRouteItem`
  concrete navigable leaf node; extends `NavigationItem` with `Route` and `PageDefinition`
- `NavigationGroupItem`
  concrete non-navigable group node; extends `NavigationItem` with `Children` (a list of `NavigationItem`)
- `PageDefinition`
  binds a detail slice factory (`Func<HttpContext, RazorSlice>`) to a navigable route
- `FeatureShellContext`
  minimal shell context passed to the app-level page renderer: current `NavigationRouteItem`, the filtered navigation tree, and current detail slice
- `IFeaturePageRenderer`
  implemented by the app; responsible for full-page shell rendering and navigation rendering
- `FeatureResultBuilder`
  fluent orchestration API for feature endpoints; used when a response may be a full page render or an HTMX fragment depending on request type; `BuildAsync()` performs authorization filtering automatically
- `HtmxFragmentResult`
  lightweight result builder for endpoints that always return a fragment, not a full feature page; supports OOB parts and triggers
- `IHasState`
  interface implemented by state models; generated automatically for any `partial` type passed to `WithState(...)`; exposes `Serialize()`, `SerializeOob()`, and `const StateId`
- `ToastTone`
  enum with semantic toast severity levels: `Info`, `Success`, `Warning`, `Error`; UI framework packages map these to visual styles

## Quick Start

Minimal service registration looks like this:

```csharp
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.Bootstrap5.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();

// App-owned page shell renderer
builder.Services.AddSingleton<IFeaturePageRenderer, DemoFeaturePageRenderer>();

// Feature discovery
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover(typeof(Program).Assembly, sp));
```

And the app pipeline:

```csharp
var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

app.UseStaticFiles();
app.UseHtmxErrorHandling(); // HTMX-aware error toasts for 4xx/5xx

features.MapEndpoints(app);
app.MapFeaturePages(features);

app.Run();
```

## Configuration

Shared configuration goes through `RazorSlicesHtmxOptions` from `RazorSlicesHtmx.AspNetCore`.

Current settings:

- `DetailTargetSelector`: HTMX target for page/detail navigation
- `DialogHostSelector`: selector used for dialog OOB updates and dialog lifecycle hooks
- `DialogClass`: CSS class applied to the dialog container
- `ToastHostSelector`: selector used for toast OOB updates and toast lifecycle hooks
- `ToastDelayMilliseconds`: toast auto-hide delay
- `DefaultOobSwap`: default HTMX OOB swap mode

Example `appsettings.json` section:

```json
{
  "RazorSlicesHtmx": {
    "DetailTargetSelector": "#details-pane",
    "DialogHostSelector": "#dialog-host",
    "DialogClass": "modal-dialog modal-dialog-centered modal-lg",
    "ToastHostSelector": "#toast-host",
    "ToastDelayMilliseconds": 3200,
    "DefaultOobSwap": "innerHTML"
  }
}
```

Example binding:

```csharp
using RazorSlicesHtmx.AspNetCore.Options;

builder.Services.Configure<RazorSlicesHtmxOptions>(
    builder.Configuration.GetSection("RazorSlicesHtmx"));

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
```

## Bootstrap5 Defaults

`AddRazorSlicesHtmxBootstrap5()` uses `PostConfigure<RazorSlicesHtmxOptions>` to apply Bootstrap-specific fallback defaults.

Today that means:

- `DialogClass` falls back to `modal-dialog modal-dialog-centered`
- `ToastDelayMilliseconds` falls back to `2600`

Those values are only used when the app has not already set them through configuration or `Configure<RazorSlicesHtmxOptions>(...)`.

## Response Target and Swap Override

Both `FeatureResultBuilder` and `HtmxFragmentResult` support `HX-Retarget` and `HX-Reswap` response headers. These let the server override **where** and **how** the response is swapped into the DOM, regardless of what the original HTMX request specified.

### `WithRetarget(selector)`

Overrides the target element on the client side. Useful when a request was aimed at one element but the response should go elsewhere — for example, returning validation errors into a form container instead of the default detail pane:

```csharp
return Result.For(detail)
    .AsFragment(formWithErrors)
    .WithRetarget("#edit-form")
    .BuildAsync();
```

### `WithReswap(swapMode)`

Overrides the swap strategy. Use any value from `HtmxSwap` (`innerHTML`, `outerHTML`, `beforeend`, etc.):

```csharp
return Result.For(detail)
    .AsFragment(appendableItem)
    .WithReswap(HtmxSwap.BeforeEnd)
    .BuildAsync();
```

### Combined — validation error pattern

The most common use case is combining both to redirect a validation failure response into the form element with a full replacement:

```csharp
// Validation failed — swap the form with errors into #edit-form using outerHTML
return Result.For(detail)
    .AsFragment(formSliceWithErrors)
    .WithRetarget("#edit-form")
    .WithReswap(HtmxSwap.OuterHtml)
    .BuildAsync();
```

Both methods are also available on `HtmxFragmentResult` for fragment-only endpoints:

```csharp
return HtmxFragmentResult.Create(formSliceWithErrors)
    .WithRetarget("#edit-form")
    .WithReswap(HtmxSwap.OuterHtml)
    .Build();
```
## Navigation Model

Navigation is built around a hierarchical tree of `NavigationItem` nodes:

```
NavigationItem (abstract)
├── NavigationRouteItem — navigable leaf with Route + PageDefinition
└── NavigationGroupItem — non-navigable group with Children
```

### Defining navigation in a feature module

Each `IFeatureModule` exposes `NavigationItems`:

```csharp
public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new NavigationRouteItem("items", "Items", "/items",
            new PageDefinition(ctx => _FeaturePage.Create(_content.CreateListModel(ctx))))
    ];

    public override void MapEndpoints(WebApplication app) { /* ... */ }
}
```

### Grouped navigation

Use `NavigationGroupItem` to create collapsible sections:

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

Groups with no authorized children are automatically excluded from the navigation tree.

### Multi-assembly discovery

Features can be spread across multiple assemblies:

```csharp
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover([typeof(Program).Assembly, typeof(SharedFeatures).Assembly], sp));
```

Duplicate assemblies are automatically deduplicated.

## Authorization

Authorization uses the standard ASP.NET Core `IAuthorizationService`. Each `NavigationItem` can declare an `AuthorizationPolicy`:

```csharp
new NavigationRouteItem("admin-users", "Users", "/admin/users", usersPage,
    AuthorizationPolicy: "AdminOnly")
```

### How it works

1. **Endpoint authorization** — feature modules add `.RequireAuthorization(policy)` to their endpoint registrations as normal.
2. **Navigation filtering** — `FeatureRegistry.CreateShellContextAsync` recursively filters the navigation tree using `IAuthorizationService.AuthorizeAsync`. Items whose policy fails are excluded. Groups with no remaining children are excluded entirely.
3. **No auth configured** — when `IAuthorizationService` is not registered or the user is null, all items pass through (backward compatible).

`BuildAsync()` handles this automatically — it reads `HttpContext.User` and resolves `IAuthorizationService` from DI:

```csharp
return Result.For(detail)
    .AsFragment(fragment)
    .BuildAsync(); // auth filtering happens here
```

`CancellationToken` is supported throughout the chain:

```csharp
return Result.For(detail)
    .AsFragment(fragment)
    .BuildAsync(cancellationToken);
```

## HTMX Error Handling

`UseHtmxErrorHandling()` adds middleware that intercepts unhandled exceptions and non-success status codes for HTMX requests. Instead of a raw error page (which HTMX ignores), it returns an error toast.

### Setup

```csharp
app.UseStaticFiles();
app.UseHtmxErrorHandling();
```

### Behaviour

- **HTMX request + exception** → catches the error, returns a toast with `ToastTone.Error`
- **HTMX request + 4xx/5xx** → returns a toast with a status-appropriate message
- **Non-HTMX request** → passes through unaffected (standard ASP.NET error handling applies)
- **Status reset** → the middleware resets the response to 200 so HTMX processes it (HTMX ignores non-2xx by default)

### Custom messages

```csharp
app.UseHtmxErrorHandling(options =>
{
    options.FormatTitle = (statusCode, ex) => statusCode switch
    {
        404 => "Nerasta",
        _ => "Klaida"
    };
    options.FormatMessage = (statusCode, ex) => statusCode switch
    {
        404 => "Prašomas puslapis nerastas.",
        _ => "Įvyko nenumatyta klaida."
    };
});
```

### Toast tones

`ToastTone` is a semantic enum: `Info`, `Success`, `Warning`, `Error`. The error middleware always uses `ToastTone.Error`. UI framework packages map the enum to their own styles:

| ToastTone | Bootstrap5 CSS |
|-----------|---------------|
| `Info` | `text-bg-info` |
| `Success` | `text-bg-success` |
| `Warning` | `text-bg-warning` |
| `Error` | `text-bg-danger` |

Usage in endpoints:

```csharp
return Result.For(detail)
    .WithToast("Item saved")                                        // default: Success
    .WithToast("Item deleted", title: "Deleted", tone: ToastTone.Warning)
    .BuildAsync();
```

## Custom UI Adapter Example

You are not required to use the Bootstrap5 package.

If your app uses another UI approach, such as Tailwind, you can provide your own `ITransientUiRenderer` and register your own fallback defaults.

Example shape:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;

public static class TailwindServiceCollectionExtensions
{
  public static IServiceCollection AddRazorSlicesHtmxTailwind(
    this IServiceCollection services,
    Action<RazorSlicesHtmxOptions>? configure = null)
  {
    if (configure is not null)
    {
      services.Configure(configure);
    }

    services.AddSingleton<IPostConfigureOptions<RazorSlicesHtmxOptions>, TailwindDefaults>();
    services.AddSingleton<ITransientUiRenderer, TailwindTransientUiRenderer>();

    return services;
  }

  private sealed class TailwindDefaults : IPostConfigureOptions<RazorSlicesHtmxOptions>
  {
    public void PostConfigure(string? name, RazorSlicesHtmxOptions options)
    {
      if (string.IsNullOrWhiteSpace(options.DialogClass))
      {
        options.DialogClass = "w-full max-w-2xl rounded-2xl bg-white shadow-2xl";
      }

      if (options.ToastDelayMilliseconds is null)
      {
        options.ToastDelayMilliseconds = 4000;
      }
    }
  }
}

public sealed class TailwindTransientUiRenderer(IOptions<RazorSlicesHtmxOptions> options)
  : ITransientUiRenderer
{
  private readonly RazorSlicesHtmxOptions _options = options.Value;

  public RazorSlice RenderDialog(RazorSlice content) =>
    TailwindDialog.Create(new DialogModel(content, _options.DialogClass ?? string.Empty));

  public RazorSlice RenderToast(ToastModel toast) =>
    TailwindToast.Create(toast);
}
```

What stays the same:

- `AspNetCore` still owns HTMX orchestration
- the app still owns the full page shell through `IFeaturePageRenderer`
- your adapter only owns transient UI concerns such as dialog and toast rendering

This is the intended extension model for non-Bootstrap apps.

## App-Owned Shell

The full page shell is intentionally outside the libraries.

An app is expected to provide:

- its own layout model
- its own page shell model
- its own Razor layout/page/sidebar slices
- an `IFeaturePageRenderer` implementation

In the demo app this is done by `DemoFeaturePageRenderer`, which turns `FeatureShellContext` into app-specific shell models and Razor slices.

This keeps branding, navigation, title formatting, and page shell decisions in the app instead of in a reusable package.

## Validation Integration

`RazorSlicesHtmx.FluentValidation` stays UI-agnostic.

It provides validation result contracts and helper methods, while Bootstrap-specific validation UI lives in `RazorSlicesHtmx.Bootstrap5`.

That split is deliberate:

- FluentValidation package: data and helper logic
- Bootstrap5 package: concrete validation markup

## What The Libraries Do Not Try To Be

These packages are not intended to be:

- a full frontend framework
- a design system
- a complete layout/navigation framework
- an opinionated admin shell

The main goal is to make HTMX feature flows, transient UI responses, and feature registration easier to compose in a RazorSlices-based app.

## Templates

### Project Template (`rshtmx-webapp`)

Scaffolds a complete RazorSlicesHtmx web application with sidebar navigation, HTMX error handling, and optional Bootstrap5/FluentValidation integration.

```bash
dotnet new install ./templates/razorsliceshtmx-webapp

# Full stack (Bootstrap5 + FluentValidation):
dotnet new rshtmx-webapp -n MyApp

# Core only (no Bootstrap5, no FluentValidation):
dotnet new rshtmx-webapp -n MyApp --bootstrap5 false --fluentValidation false
```

| Parameter            | Description                        | Default    |
|----------------------|------------------------------------|------------|
| `-n`, `--name`       | Project name                       | `RshtmxApp` |
| `--bootstrap5`       | Include Bootstrap5 UI adapter      | `true`     |
| `--fluentValidation` | Include FluentValidation           | `true`     |
| `--framework`        | Target framework                   | `net10.0`  |

The generated project includes AI development support:
- `.github/copilot-instructions.md` — project conventions for Copilot
- `.github/prompts/create-feature.prompt.md` — scaffold new features
- `.github/prompts/add-list.prompt.md` — add list pages
- `.github/prompts/add-form.prompt.md` — add forms
- `.github/prompts/add-endpoint.prompt.md` — add HTMX endpoints

### Feature Module Template (`rshtmx-feature`)

Scaffolds a feature module with endpoints, models, services, and Razor slices into an existing project.

```bash
dotnet new install ./templates/razorsliceshtmx-feature
dotnet new rshtmx-feature -n Products --entity Product --rootNamespace MyApp --route /products --order 200
```

This generates:

```
Products/
├── Endpoints/ProductsEndpoints.cs    # IFeatureModule with list + create endpoints
├── Models/
│   ├── ProductRowModel.cs            # Row model record
│   ├── ProductsListModel.cs          # List model record
│   └── ProductUpsertRequest.cs       # Form request with [GenerateHtmlNames]
├── Services/ProductsContentService.cs # Navigation item + sample data
└── Slices/
    ├── _FeaturePage.cshtml           # Full page with table + create button
    ├── _ListPage.cshtml              # HTMX fragment for list refresh
    ├── _CreatePage.cshtml            # Create form
    └── _ViewImports.cshtml           # Namespace imports
```

| Parameter        | Description                          | Default         |
|------------------|--------------------------------------|-----------------|
| `-n`, `--name`   | Feature name (e.g. `Products`)       | `MyFeature`     |
| `--entity`       | Entity name (e.g. `Product`)         | Same as feature |
| `--rootNamespace` | Root namespace of the project       | `MyApp`         |
| `--route`        | Base route (e.g. `/products`)        | `/my-feature-route` |
| `--order`        | Navigation order (lower = first)     | `100`           |

After scaffolding, ensure the feature assembly is included in `FeatureRegistry.Discover()`.

## Demo Reference

If you want a concrete reference, look at `demo/RazorSlicesHtmx.Demo` for:

- feature registration
- app-owned page shell rendering (`DemoFeaturePageRenderer`)
- Bootstrap5 transient UI integration
- FluentValidation integration
- vertical slice examples including CRUD and nested HTMX interactions

The demo contains three feature modules:

- **Items** (`Features/Items`)
  Full CRUD feature using `BaseFeatureModule`; demonstrates paged list with search and sort, create/edit dialogs, delete confirmation dialog, toasts, FluentValidation, and state preservation across HTMX interactions.

- **Form** (`Features/Form`)
  Live form preview feature using `IFeatureModule` directly; demonstrates a POST endpoint that returns a rendered preview fragment without a full-page reload.

- **HowTo** (`Features/HowTo`)
  Step-by-step content feature using `IFeatureModule` directly; demonstrates `HtmxFragmentResult` with an OOB update to the pill navigation when the active step changes.