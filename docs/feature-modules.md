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
        new NavigationRouteItem("items", "Items", "/items",
            new PageDefinition(ctx => _FeaturePage.Create(_content.CreateListModel(ctx))))
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items/list", ([AsParameters] ItemSearchModel search, ItemListQuery query, AppDbContext db) =>
        {
            var model = _content.CreateListModel(db, search, query);
            return Result.Page(_FeaturePage.Create(model))
                .WithState(search)
                .WithState(query)
                .Build();
        });
    }
}
```

`BaseFeatureModule` provides the `Result` convenience facade backed by `FeatureResultBuilder`. Implement `IFeatureModule` directly only when you don't use `FeatureResultBuilder`.

## Navigation tree

`NavigationItems` returns a list of `NavigationItem` nodes. Two concrete types exist:

| Type | Purpose |
|------|---------|
| `NavigationRouteItem` | Navigable leaf — has `Route`, `PageDefinition`, optional `AuthorizationPolicy` |
| `NavigationGroupItem` | Non-navigable group — has `Children`, optional `AuthorizationPolicy` |

```csharp
// Leaf
new NavigationRouteItem("items", "Items", "/items", pageDef)

// Group
new NavigationGroupItem("admin", "Administration",
[
    new NavigationRouteItem("users", "Users", "/admin/users", usersPage),
    new NavigationRouteItem("roles", "Roles", "/admin/roles", rolesPage)
])
```

Groups with no authorized children are automatically hidden.

## PageDefinition

Binds a slice factory to a navigable route. `MapFeaturePages(features)` registers one GET endpoint per `NavigationRouteItem` using this factory:

```csharp
// With HttpContext (for context-aware initialization)
new PageDefinition(ctx => _FeaturePage.Create(_content.CreateModel(ctx)))

// Without context
new PageDefinition(() => _FeaturePage.Create())
```

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
