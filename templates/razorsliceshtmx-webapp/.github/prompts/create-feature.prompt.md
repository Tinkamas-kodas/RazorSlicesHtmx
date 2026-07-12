---
agent: agent
description: "Scaffold a new RazorSlicesHtmx feature module"
---

# Create a New Feature Module

Create a new feature module following the project's vertical slice architecture.

## Ask the user for:
1. **Feature name** (e.g., "Products", "Orders")
2. **Entity name** if different from feature (e.g., "Product" for "Products" feature)
3. **Route** (e.g., "/products")
4. **Navigation order** (lower = appears first in sidebar)

## Create these files:

### `Features/{FeatureName}/Endpoints/{FeatureName}Endpoints.cs`
```csharp
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using {RootNamespace}.Features.{FeatureName}.Services;
using {RootNamespace}.Features.{FeatureName}.Slices;

namespace {RootNamespace}.Features.{FeatureName}.Endpoints;

public sealed class {FeatureName}Endpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly {FeatureName}ContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("{featurename-lowercase}", "{FeatureName}", "{route}", Order: {order})
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("{route}", () => Result.Page(_{FeatureName}Page.Create()));
    }
}
```

### `Features/{FeatureName}/Services/{FeatureName}ContentService.cs`
```csharp
namespace {RootNamespace}.Features.{FeatureName}.Services;

public sealed class {FeatureName}ContentService
{
    // Content creation methods go here
}
```

### `Features/{FeatureName}/Slices/_{FeatureName}Page.cshtml`
```razor
@inherits RazorSlice

<article class="section-panel">
    <h2>{FeatureName}</h2>
    <p>Welcome to the {FeatureName} feature.</p>
</article>
```

### `Features/{FeatureName}/Slices/_ViewImports.cshtml`
```razor
@using {RootNamespace}.Features.{FeatureName}.Models
@using {RootNamespace}.Features.{FeatureName}.Slices
```

## Important:
- The feature is auto-discovered by `FeatureRegistry.Discover()` — no manual registration needed
- Use the project's root namespace from the .csproj `<RootNamespace>` element
- Follow existing code style in the project
- Page title defaults to `NavigationItem.Label`; for specific screens use `Result.Page(...).WithPageTitle("...")`
