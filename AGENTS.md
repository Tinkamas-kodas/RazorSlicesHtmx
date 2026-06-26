# AGENTS.md — RazorSlicesHtmx

This file provides context for AI agents working with the RazorSlicesHtmx codebase.

## What is this project?

RazorSlicesHtmx is a .NET library for building server-rendered HTMX applications with ASP.NET Core.
It provides feature module discovery, hierarchical navigation with authorization filtering,
HTMX response composition, and Roslyn source generators for forms and state management.

## Repository Structure

```
src/
  RazorSlicesHtmx.AspNetCore/         # Core library
    Extensions/                        # DI and middleware registration
    Features/                          # IFeatureModule, BaseFeatureModule, FeatureRegistry
    Infrastructure/                    # HTMX request helpers, error middleware
    Models/                            # NavigationItem, PageDefinition, ToastModel, ToastTone
    Options/                           # RazorSlicesHtmxOptions
    Rendering/                         # IFeaturePageRenderer, ITransientUiRenderer contracts
    Results/                           # FeatureResultBuilder, HtmxFragmentResult, SliceResults
    Slices/                            # Technical Razor slices (_Empty, HtmxFragment, HtmxOob)
  RazorSlicesHtmx.Bootstrap5/         # Bootstrap 5 UI adapter
  RazorSlicesHtmx.FluentValidation/   # Validation error mapping
  RazorSlicesHtmx.Generators/         # Roslyn source generators
demo/
  RazorSlicesHtmx.Demo/               # Reference demo application
tests/
  RazorSlicesHtmx.AspNetCore.Tests/   # Core library tests (93 tests)
  RazorSlicesHtmx.FluentValidation.Tests/
```

## Build and Test

```bash
dotnet build
dotnet test
```

Target frameworks: `net8.0` + `net10.0`. Tests target `net10.0` only.
Package versions managed centrally via `Directory.Packages.props`.

## Core Concepts

### Navigation Model

Navigation is a tree of `NavigationItem` nodes:

- `NavigationItem` — abstract base with `Key`, `Label`, `Order`, `AuthorizationPolicy`
- `NavigationRouteItem` — navigable leaf with `Route` and `PageDefinition`
- `NavigationGroupItem` — non-navigable group with `Children`

### Feature Modules

Each feature implements `IFeatureModule` (or extends `BaseFeatureModule`):

```csharp
public sealed class MyFeature(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems => [
        new NavigationRouteItem("my-feature", "My Feature", "/my-feature",
            new PageDefinition(ctx => _Page.Create(model)))
    ];

    public override void MapEndpoints(WebApplication app) { /* endpoints */ }
}
```

Feature folder convention: `Features/{Name}/Endpoints/`, `Models/`, `Services/`, `Slices/`, `Validators/`.

### HTMX Response Building

Two builders exist:

1. **`FeatureResultBuilder`** — for feature page endpoints (auto-detects HTMX vs full page).
   Uses `BuildAsync()` (async, includes auth filtering). No sync `Build()`.

2. **`HtmxFragmentResult`** — for fragment-only endpoints.
   Uses `Build()` (sync, no auth).

Common fluent API: `.AsFragment()`, `.WithOob()`, `.WithState()`, `.WithToast()`,
`.WithTrigger()`, `.WithNavigation()`, `.WithLocation()`, `.WithRetarget()`, `.WithReswap()`,
`.WithDialog()`, `.ClearDialog()`.

### Authorization

`NavigationItem.AuthorizationPolicy` declares required policy. `FeatureRegistry.CreateShellContextAsync`
filters the tree via `IAuthorizationService`. Groups with no visible children are excluded.
Endpoint-level auth uses standard `.RequireAuthorization()`.

### Toast Tones

Use `ToastTone` enum: `Info`, `Success`, `Warning`, `Error`. Never use raw strings.
Bootstrap5 maps: `Error→"danger"`, `Success→"success"`, etc.

### Error Middleware

`app.UseHtmxErrorHandling()` — intercepts HTMX 4xx/5xx and exceptions, returns error toasts.
Configure via `HtmxErrorOptions` (`FormatTitle`, `FormatMessage`).

### State Management

`partial` classes/records passed to `WithState()` get `IHasState` generated automatically.
No attribute needed — the generator detects call sites. Produces `StateId`, `Serialize()`, `SerializeOob()`.

### Source Generators

- `[GenerateHtmlNames]` — field name/id/path constants for form models
- State generator — auto-implements `IHasState` for `WithState()` arguments
- List request binder — `FromQuery`, `BindAsync`, `SortMap` for `ListRequest<T>` subclasses

## Common Pitfalls

1. `FeatureResultBuilder` has only `BuildAsync()`, not `Build()`.
2. `FeatureMetadata` no longer exists — replaced by `NavigationRouteItem`.
3. `PageDefinition` takes `Func<HttpContext, RazorSlice>` or `Func<RazorSlice>`.
4. `IFeatureModule.NavigationItems` returns `IReadOnlyList<NavigationItem>`.
5. `Microsoft.Extensions.Options.Options.Create(...)` must be fully qualified in tests (conflicts with `RazorSlicesHtmx.AspNetCore.Options` namespace).
6. `RazorSlice` abstract method is `ExecuteAsync()` (no params). Test fakes must override it.
7. The app owns the page shell via `IFeaturePageRenderer` — the library does not provide layouts.
