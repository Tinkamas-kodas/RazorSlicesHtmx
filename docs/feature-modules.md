# Feature Modules

A feature module is a self-contained unit that registers navigation items and maps its own endpoints.

## Defining a feature module

Extend `BaseFeatureModule` and inject `FeatureResultBuilder`:

```csharp
public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("items", "Items", "/items", Order: 100)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items", ([AsParameters] ItemSearchModel search, ItemListQuery query, AppDbContext db) =>
        {
            var model = _content.CreateListModel(db, search, query);
            return Result.Page(_FeaturePage.Create(model))
                .WithState(search)
                .WithState(query);
        });
    }
}
```

`BaseFeatureModule` provides the `Result` convenience facade backed by `FeatureResultBuilder`. Implement `IFeatureModule` directly only when you don't use `FeatureResultBuilder`.

## Navigation tree

`NavigationItems` returns a list of `NavigationItem` nodes. Two concrete types exist:

| Type | Purpose |
|------|---------|
| `NavigationItem` | Navigable leaf — has `Key`, `Label`, `Route`, `Order`, optional `AuthorizationPolicy` |
| `NavigationGroupItem` | Non-navigable group — inherits `NavigationItem`, adds `Children` |

```csharp
// Leaf
new NavigationItem("items", "Items", "/items")

// Group
new NavigationGroupItem("admin", "Administration",
[
    new NavigationItem("users", "Users", "/admin/users"),
    new NavigationItem("roles", "Roles", "/admin/roles")
])
```

Groups with no authorized children are automatically hidden.

## Feature discovery

`FeatureRegistry.Discover` scans assemblies for `IFeatureModule` implementations and registers them:

```csharp
// Single assembly
FeatureRegistry.Discover(typeof(Program).Assembly, sp)

// Multiple assemblies — duplicates are automatically deduplicated
FeatureRegistry.Discover([typeof(Program).Assembly, typeof(SharedLib).Assembly], sp)
```

## Folder conventions

```
Features/{FeatureName}/
  Endpoints/{FeatureName}Endpoints.cs  — feature module class
  Models/                               — view models, row models, request models
  Services/{FeatureName}ContentService.cs
  Slices/                               — Razor slices (.cshtml)
  Validators/                           — FluentValidation validators
```

## See also

- [Navigation & URL](navigation-url.md)
- [Authorization](authorization.md)
- [Response Building](response-building.md)
- [Getting Started](getting-started.md)
