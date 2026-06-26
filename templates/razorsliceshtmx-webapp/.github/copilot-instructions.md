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

## Key Patterns

### Feature Module
Every feature extends `BaseFeatureModule(FeatureResultBuilder)`:
```csharp
public sealed class MyEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems => [...];
    public override void MapEndpoints(WebApplication app) { ... }
}
```

### Building Results
Always use `FeatureResultBuilder` — never return raw `IResult`:
```csharp
// Full page (detail swap + navigation update):
Result.For(_PageSlice.Create(model)).BuildAsync()

// HTMX fragment (detail swap only):
Result.For(_PageSlice.Create(model)).AsFragment(_FragmentSlice.Create(model)).BuildAsync()

// With toast notification:
Result.For(_Empty.Create()).WithToast("Saved!").BuildAsync()

// With HTMX trigger:
Result.For(_Empty.Create()).WithTrigger("Items.ListRefresh").BuildAsync()
```

### Navigation Model
- `NavigationRouteItem(key, label, route, pageDefinition, Order)` — leaf with route
- `NavigationGroupItem(key, label, children, Order)` — folder/group
- `PageDefinition(() => slice)` — lazy factory for full-page rendering

### RazorSlice Conventions
- File names start with `_` (e.g., `_ListPage.cshtml`)
- Use `@inherits RazorSlice<TModel>` for typed models
- Use `@inherits RazorSlice` for no-model slices
- Each feature's Slices/ folder has `_ViewImports.cshtml` with model namespace

### HTMX Conventions
- `hx-get`/`hx-post` point to feature routes
- `hx-target` uses `detailTargetSelector` from options for detail pane
- `hx-swap` uses `HtmxSwap.InnerHtml` or `HtmxSwap.OuterHtml`
- `hx-push-url="true"` for navigation links

## Rules
- Never create controllers — use minimal API endpoints in `MapEndpoints`
- Never reference `Microsoft.AspNetCore.Mvc` — use RazorSlices only
- All endpoints return `Task<IResult>` via `BuildAsync()`
- Models are `sealed record` types
- `[GenerateHtmlNames]` attribute on form request models (source-generated)

## Available Prompt Skills
Use these via Copilot chat:
- `/create-feature` — scaffold a new feature module
- `/add-list` — add a list page with table to a feature
- `/add-form` — add a create/edit form to a feature
- `/add-endpoint` — add a new HTMX endpoint to a feature
