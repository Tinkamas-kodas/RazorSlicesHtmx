# AGENTS.md — RazorSlicesHtmx

This file provides context for AI agents working with the RazorSlicesHtmx codebase.

## What is this project?

RazorSlicesHtmx is a .NET library for building server-rendered HTMX applications with ASP.NET Core.
It provides feature module discovery, hierarchical navigation with authorization filtering,
HTMX response composition, and Roslyn source generators for forms and state management.

The `Apskaita/` subfolder is a separate application built on this library — for its conventions,
see [`Apskaita/AGENTS.md`](Apskaita/AGENTS.md).

## Repository Structure

```
src/
  RazorSlicesHtmx.AspNetCore/         # Core library
    Extensions/                        # DI and middleware registration
    Features/                          # IFeatureModule, BaseFeatureModule, FeatureRegistry
    Infrastructure/                    # HTMX request helpers, error middleware
    Models/                            # NavigationItem, ToastModel, ToastTone
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

- `NavigationItem` — abstract base with `Key`, `Label`, `Route`, `Order`, `AuthorizationPolicy`
- `NavigationGroupItem` — non-navigable group with `Children` (inherits `NavigationItem` with empty `Route`)

```csharp
// Leaf (navigable)
new NavigationItem("my-feature", "My Feature", "/my-feature", Order: 100)

// Group
new NavigationGroupItem("admin", "Administration",
[
    new NavigationItem("users", "Users", "/admin/users"),
    new NavigationItem("roles", "Roles", "/admin/roles")
])
```

Page rendering is done by mapping the route in `MapEndpoints` — there is no `PageDefinition`. `MapFeaturePages` only registers `GET /` redirect.

### Feature Modules

Each feature implements `IFeatureModule` (or extends `BaseFeatureModule`):

```csharp
public sealed class MyFeature(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems => [
        new("my-feature", "My Feature", "/my-feature", Order: 100)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/my-feature", () => Result.Page(_Page.Create(model)));
    }
}
```

Feature folder convention: `Features/{Name}/Endpoints/`, `Models/`, `Services/`, `Slices/`, `Validators/`.

### HTMX Response Building

Two builders exist:

1. **`FeatureResultBuilder`** — for feature page endpoints (auto-detects HTMX vs full page).
   Uses `Build()` (async, includes auth filtering). No sync `Build()`.

2. **`HtmxFragmentResult`** — for fragment-only endpoints.
   Uses `Build()` (sync, no auth).

`.WithTrigger()`, `.WithNavigation()`, `.WithLocation()`, `.WithRetarget()`, `.WithReswap()`,
`.WithDialog()`, `.ClearDialog()`, `.WithPageTitle()`.

### Page Title Flow

- Default page title is derived from `NavigationItem.Label`.
- `FeatureResultBuilder.WithPageTitle("...")` overrides title for that specific response.
- Full-page rendering consumes title via `FeatureShellContext.PageTitle`.
- HTMX rendering emits OOB `<title hx-swap-oob="true">...</title>` so browser tab title updates on fragment responses.

### Authorization

`NavigationItem.AuthorizationPolicy` declares required policy. `FeatureRegistry.CreateShellContext`
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
- `[GenerateFormBinder]` — generate `FromForm(...)` and `BindAsync(...)` for partial upsert/form models
- State generator — auto-implements `IHasState` for `WithState()` arguments
- List request binder — `FromQuery`, `BindAsync`, `SortMap` for `ListRequest<T>` subclasses

### Form Binding

- Prefer `[GenerateFormBinder]` on `partial` create/edit request models that implement `IHaveValidationResult`.
- Use generated `BindAsync(...)` when the endpoint should accept the model directly.
- Keep `ListRequest<T>` on the query-string path; its generator still owns `FromQuery(...)` and paging/sort binding.

### Bootstrap 5 Form Helpers

Form field helpers in `RazorSlicesHtmx.Bootstrap5.Extensions` return `FluentTagBuilder` (except `.Validation()`) and support fluent chaining:

```cshtml
@using MyApp.HtmlNames.MyApp.Features.Items.Models
@{ var f = Model.For(); }
@{ var fo = BootstrapFieldOptions.Floating; }

<div class="form-floating">
    @f.Code.Input(placeholder: "")
    @f.Code.Label("Code", fo)
    @f.Code.Validation()
</div>

@* Fluent chaining *@
@f.Name.Input().WithClass("border-primary")
@f.Name.Label("Name").WithClass("fw-bold")
```

- Use `BootstrapFieldOptions.Floating` (preset with empty `LabelClass`) for floating label layouts.
- For floating labels: place `Input` **before** `Label`, wrap in `<div class="form-floating">`.
- `[GenerateHtmlNames]` is declared (not generated) in the `RazorSlicesHtmx.AspNetCore` assembly, namespace `RazorSlicesHtmx.AspNetCore.Html` — one shared type, so every consuming assembly resolves the same attribute. Add `global using RazorSlicesHtmx.AspNetCore.Html;` to use it unqualified.

## Common Pitfalls

1. `FeatureResultBuilder` has only `Build()`, not `Build()`.
2. `FeatureMetadata` no longer exists — use `NavigationItem` instead.
3. `PageDefinition` is removed — map routes directly in `MapEndpoints`.
4. `IFeatureModule.NavigationItems` returns `IReadOnlyList<NavigationItem>`.
5. `Microsoft.Extensions.Options.Options.Create(...)` must be fully qualified in tests (conflicts with `RazorSlicesHtmx.AspNetCore.Options` namespace).
6. `RazorSlice` abstract method is `ExecuteAsync()` (no params). Test fakes must override it.
7. The app owns the page shell via `IFeaturePageRenderer` — the library does not provide layouts.
8. If `WithPageTitle(...)` is not used, title falls back to `NavigationItem.Label`.
