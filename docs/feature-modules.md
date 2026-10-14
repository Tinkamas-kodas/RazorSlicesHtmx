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
        new("items", "Items", "list", Order: 100)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", ([AsParameters] ItemSearchModel search, ItemListQuery query, AppDbContext db) =>
        {
            var model = _content.CreateListModel(db, search, query);
            return Result.Page(_FeaturePage.Create(model))
                .WithState(search)
                .WithState(query);
        });
    }
}
```

Register each feature and its route-group prefix in startup:

```csharp
builder.Services.Configure<RazorSlicesHtmxOptions>(options =>
{
    options.FeatureRegistration
        .AddFeature<ItemsEndpoints>("items");
});
```

With this setup, feature endpoints stay relative (`"list"`, `"create"`, `"edit/{id}"`) and startup owns the absolute prefix (`/items`).

`BaseFeatureModule` provides the `Result` convenience facade backed by `FeatureResultBuilder`. Implement `IFeatureModule` directly only when you don't use `FeatureResultBuilder`.

For features that use a workspace wrapper, inherit `BaseFeatureModuleWithWorkspace<TWorkspaceFactory>` and define `FeatureDivId`:

```csharp
public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder)
    : BaseFeatureModuleWithWorkspace<ItemsWorkspaceFactory>(resultBuilder)
{
    public override string FeatureDivId => "feature-details";
}
```

In workspace features, use `Result.Page(...)` for feature-level screens and `Result.Fragment(...)` for inner targets (such as list/table refreshes).

## Composing large features

A module that grows beyond a few routes can be composed from collaborator classes instead of one large
`MapEndpoints`. `BaseFeatureModule` exposes the pieces needed:

| Member | Purpose |
|--------|---------|
| `Result` | The `ModuleResultFacade` (public type) — pass it to a section so it can build pages/fragments/dialogs |
| `ResultBuilder` | The underlying `FeatureResultBuilder`, when a collaborator wants the full builder |
| `MapSubgroup(group, name)` | Creates a nested route group under the feature's prefix; inherits the parent's policies |

```csharp
public sealed partial class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("items", "Items", "list", Order: 100)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AppPolicies.Operational);

        new ListSection(Result).Map(group);                       // /items/list
        new DialogSection(Result).Map(MapSubgroup(group, "dialog")); // /items/dialog/...
    }
}

// A section is a plain class holding the facade — no inheritance, no nesting required.
public sealed class ListSection(BaseFeatureModule.ModuleResultFacade result)
{
    public void Map(RouteGroupBuilder group) =>
        group.MapGet("list", () => result.Fragment(_List.Create(model)));
}
```

Keep one navigation item per feature: register a single module per prefix and let its sections share it.
A module with no `NavigationItems` cannot produce `Result.Page(...)` or `Result.Fragment(...)` — the
facade needs a route item to resolve the page shell.

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

## Feature registration

Preferred approach:

```csharp
builder.Services.Configure<RazorSlicesHtmxOptions>(options =>
{
    options.FeatureRegistration
        .AddFeature<ItemsEndpoints>("items");
});
```

Optional fallback:

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
