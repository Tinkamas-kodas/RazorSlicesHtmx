# Copilot Instructions — RazorSlicesHtmx

## Project Overview

RazorSlicesHtmx is a server-rendered HTMX toolkit for ASP.NET Core built around RazorSlices.
It provides feature module discovery, hierarchical navigation with authorization, HTMX response
composition (OOB swaps, triggers, toasts, dialogs), and source generators for forms and state.

## Architecture

```
src/
  RazorSlicesHtmx.AspNetCore     — core: features, navigation, HTMX results, error middleware
  RazorSlicesHtmx.Bootstrap5     — Bootstrap 5 adapter: toasts, dialogs, pager, sort headers
  RazorSlicesHtmx.FluentValidation — FluentValidation → error dictionary mapping
  RazorSlicesHtmx.Generators     — Roslyn generators: HtmlNames, State, ListRequest binder
demo/
  RazorSlicesHtmx.Demo           — demo app (reference implementation)
tests/
  RazorSlicesHtmx.AspNetCore.Tests
  RazorSlicesHtmx.FluentValidation.Tests
```

Targets: `net8.0` + `net10.0`. Tests target `net10.0` only.

## Key Conventions

### Feature Module Structure

Every feature follows this folder layout:

```
Features/{FeatureName}/
  Endpoints/{FeatureName}Endpoints.cs   — endpoint class extending BaseFeatureModule
  Models/                                — view models, row models, request models
  Services/{FeatureName}ContentService.cs — business logic, model creation
  Slices/                                — Razor slices (.cshtml)
  Validators/                            — FluentValidation validators
```

### Creating a Feature Module

```csharp
public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("items", "Items", "/items", Order: 200)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items", ([AsParameters] SearchModel search, ListQuery query) =>
        {
            var model = _content.CreateListModel(db, search, query);
            return Result.Page(_FeaturePage.Create(model))
                .WithState(search)
                .WithState(query);
        });
    }
}
```

### Navigation Model

```
NavigationItem (record)
└── NavigationGroupItem — group container: Key, Label, Children, Order, AuthorizationPolicy
```

- `NavigationItem(key, label, route, order?, authPolicy?)` — navigable leaf.
- `NavigationGroupItem(key, label, children, order?, authPolicy?)` — non-navigable group container.
- `AuthorizationPolicy` is optional — when set, the item is filtered via `IAuthorizationService`.
- Groups with no authorized children are automatically excluded.
- **`PageDefinition` is removed** — page rendering is done by mapping the route in `MapEndpoints`.

### HTMX Response Patterns

**Full page or fragment (auto-detect):**
```csharp
return Result.For(detail)           // full-page detail slice
    .WithState(search)               // OOB state update
    .WithToast("Saved")              // toast notification
    .WithTrigger("Items.ListRefresh") // client-side event
    .Build();  // includes auth filtering
```

**Fragment-only (no page shell):**
```csharp
return HtmxFragmentResult.Create(primary)
    .WithOob(oobPart)
    .WithTrigger("refresh")
    .WithRetarget("#form")
    .WithReswap(HtmxSwap.OuterHtml)
    .Build();
```

**Dialog:**
```csharp
return Result.Dialog(_DeleteConfirm.Create(model));
```

### Toast Tones

Use the `ToastTone` enum — never raw strings:

- `ToastTone.Info`
- `ToastTone.Success` (default for `WithToast`)
- `ToastTone.Warning`
- `ToastTone.Error`

Bootstrap5 maps these to CSS: `Info→info`, `Success→success`, `Warning→warning`, `Error→danger`.

### State Management

State models must be `partial` — the source generator adds `IHasState`:

```csharp
public sealed partial record ItemSearchModel(string? Search);
```

- Call `WithState(model)` to include OOB state in HTMX responses.
- Use `[StateNotMap]` to exclude properties from serialization.
- Reference `ItemSearchModel.StateId` (const) in `hx-include`.

### List Features

```csharp
public sealed partial class ItemListQuery : ListRequest<ItemRowModel>
{
    private const string DefaultSortBy = "code";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 5;
}
```

The generator creates `FromQuery`, `BindAsync`, and `SortMap`. Use `[SortDisable]` and
`[CustomSortExpression]` on row model properties to customize sort keys.

### Form Field Names

```csharp
[GenerateHtmlNames]
public sealed class ItemUpsertRequest { ... }
```

Note: `[GenerateHtmlNames]` is in namespace `RazorSlicesHtmx.AspNetCore.HtmlNames`.

Use `For.Code.Name`, `For.Code.Id` in Razor for compile-time safe form field references.

For instance context with Bootstrap helpers:

```cshtml
@using MyApp.HtmlNames.MyApp.Features.Items.Models
@{ var f = Model.For(); }
@{ var fo = BootstrapFieldOptions.Floating; }

<div class="form-floating">
    @f.Code.Input(placeholder: "")
    @f.Code.Label("Code", fo)
    @f.Code.Validation()
</div>
```

Helpers return `FluentTagBuilder` (chainable) except `.Validation()`. Use `BootstrapFieldOptions.Floating` preset for floating label layout.

## Service Registration

```csharp
builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
builder.Services.AddSingleton<IFeaturePageRenderer, MyPageRenderer>();
builder.Services.AddSingleton(sp => FeatureRegistry.Discover(typeof(Program).Assembly, sp));
```

Multi-assembly:
```csharp
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover([typeof(Program).Assembly, typeof(SharedLib).Assembly], sp));
```

## App Pipeline

```csharp
app.UseStaticFiles();
app.UseHtmxErrorHandling();    // HTMX error → toast (before endpoint routing)
features.MapEndpoints(app);     // feature-local endpoints
app.MapFeaturePages(features);  // registers GET / redirect to default route
```

## Error Handling

`UseHtmxErrorHandling()` catches exceptions and 4xx/5xx for HTMX requests, returning toasts.
Non-HTMX requests pass through. Customize with `HtmxErrorOptions`:

```csharp
app.UseHtmxErrorHandling(o =>
{
    o.FormatTitle = (code, ex) => code >= 500 ? "Server Error" : "Error";
    o.FormatMessage = (code, ex) => "Something went wrong.";
});
```

## Testing

- Tests use xUnit. Run with `dotnet test`.
- Use `DefaultHttpContext` with `MemoryStream` as response body for result testing.
- Use `FakeSlice : RazorSlice, IResult` pattern — implements both for builder testing.
- `Options.Create(...)` must be fully qualified as `Microsoft.Extensions.Options.Options.Create(...)`.

## Important Rules

1. **Always use `Build()`** — `Build()` does not exist on `FeatureResultBuilder`. Only `HtmxFragmentResult` has sync `Build()`.
3. **`PageDefinition` is removed** — map page routes directly in `MapEndpoints`.
4. **Toast tones are `ToastTone` enum** — not strings. Bootstrap5 maps enum → CSS class.
5. **`IFeatureModule.NavigationItems`** — returns `IReadOnlyList<NavigationItem>`, not a single `PageDefinition`.
6. **Central package management** — versions are in `Directory.Packages.props`.
7. **The app owns the page shell** — `IFeaturePageRenderer` is implemented by the app, not the library.
